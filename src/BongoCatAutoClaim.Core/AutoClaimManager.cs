using System.Diagnostics;

namespace BongoCatAutoClaim.Core;

public sealed class AutoClaimManager
{
    private readonly CompatibilityCatalog catalog;
    private readonly BackupManager backups;
    private readonly PatchEngine patchEngine;
    private readonly DiagnosticLog log;
    private readonly Func<bool> isGameRunning;

    public AutoClaimManager(CompatibilityCatalog catalog, AppStorage storage, Func<bool>? isGameRunning = null)
    {
        this.catalog = catalog;
        backups = new BackupManager(storage);
        patchEngine = new PatchEngine();
        log = new DiagnosticLog(storage);
        this.isGameRunning = isGameRunning ?? (() => Process.GetProcessesByName("BongoCat").Length != 0);
    }

    public InstallationStatus GetStatus(GameInstallation installation)
    {
        if (!File.Exists(installation.AssemblyPath))
            return new InstallationStatus(installation, AssemblyState.Missing, null, null, "Assembly-CSharp.dll was not found.");

        var hash = FileHash.Sha256(installation.AssemblyPath);
        var definition = catalog.FindByHash(hash);
        if (definition is null)
            return new InstallationStatus(installation, AssemblyState.Unknown, hash, null,
                "Unsupported Assembly-CSharp.dll. This build must be manually audited before it can be patched.");

        if (installation.SteamBuildId is not null && installation.SteamBuildId != definition.SteamBuildId)
            return new InstallationStatus(installation, AssemblyState.Unknown, hash, definition,
                $"Assembly hash is known, but Steam reports unexpected build {installation.SteamBuildId}.");

        if (hash.Equals(definition.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            return new InstallationStatus(installation, AssemblyState.SupportedOriginal, hash, definition,
                $"Supported original detected ({definition.DisplayName}). Ready to install.");

        return new InstallationStatus(installation, AssemblyState.AcceptedPatched, hash, definition,
            $"Bongo Cat Auto Claim is installed and matches the runtime-accepted build ({definition.DisplayName}).");
    }

    public OperationResult Install(GameInstallation installation)
    {
        EnsureGameStopped();
        var status = GetStatus(installation);
        if (status.AssemblyState == AssemblyState.AcceptedPatched)
            return new OperationResult(true, "Auto Claim is already installed and verified.", status.Compatibility is null ? null : TryBackupPath(status.Compatibility));
        if (status.AssemblyState != AssemblyState.SupportedOriginal || status.Compatibility is null)
            throw new InvalidOperationException(status.Message);

        var definition = status.Compatibility;
        patchEngine.VerifyOriginalAssembly(installation.AssemblyPath, definition);
        var backup = backups.EnsurePristineBackup(installation.AssemblyPath, definition);
        log.Write($"Verified pristine backup for Steam build {definition.SteamBuildId}; SHA-256 {definition.OriginalSha256}.");

        var candidate = Path.Combine(Path.GetDirectoryName(installation.AssemblyPath)!, "Assembly-CSharp.autoclaim.candidate.dll");
        try
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
            patchEngine.CreatePatchedAssembly(installation.AssemblyPath, candidate, definition);
            File.Move(candidate, installation.AssemblyPath, overwrite: true);
            var installedHash = FileHash.Sha256(installation.AssemblyPath);
            if (!installedHash.Equals(definition.AcceptedPatchedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Installed assembly did not match the accepted patched SHA-256.");
            log.Write($"Installed patch for Steam build {definition.SteamBuildId}; SHA-256 {installedHash}.");
            return new OperationResult(true, "Auto Claim was installed and verified successfully.", backup);
        }
        finally
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }

    public OperationResult Restore(GameInstallation installation)
    {
        EnsureGameStopped();
        var status = GetStatus(installation);
        if (status.Compatibility is null || status.AssemblyState == AssemblyState.Unknown)
            throw new InvalidOperationException("Restore refused because the installed assembly is not a recognized supported state.");
        if (status.AssemblyState == AssemblyState.SupportedOriginal)
            return new OperationResult(true, "The verified original assembly is already installed.", TryBackupPath(status.Compatibility));

        var definition = status.Compatibility;
        var backup = backups.GetVerifiedBackup(definition);
        patchEngine.VerifyOriginalAssembly(backup, definition);
        var candidate = Path.Combine(Path.GetDirectoryName(installation.AssemblyPath)!, "Assembly-CSharp.autoclaim.restore.dll");
        try
        {
            File.Copy(backup, candidate, overwrite: true);
            File.Move(candidate, installation.AssemblyPath, overwrite: true);
            if (!FileHash.Sha256(installation.AssemblyPath).Equals(definition.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Restored assembly failed SHA-256 verification.");
            log.Write($"Restored pristine assembly for Steam build {definition.SteamBuildId}; SHA-256 {definition.OriginalSha256}.");
            return new OperationResult(true, "The pristine original was restored and verified.", backup);
        }
        finally
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }

    private string? TryBackupPath(CompatibilityDefinition definition)
    {
        try { return backups.GetVerifiedBackup(definition); }
        catch (FileNotFoundException) { return null; }
    }

    private void EnsureGameStopped()
    {
        if (isGameRunning())
            throw new InvalidOperationException("Bongo Cat is running. Close it normally before installing or restoring.");
    }
}
