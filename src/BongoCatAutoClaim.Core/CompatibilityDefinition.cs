namespace BongoCatAutoClaim.Core;

public sealed record CompatibilityDefinition(
    string SteamAppId,
    string SteamBuildId,
    string DisplayName,
    string OriginalSha256,
    string AcceptedPatchedSha256,
    Guid ModuleMvid,
    string PatchDefinition,
    int StockRefreshSeconds,
    string RuntimeAcceptance);

public enum AssemblyState
{
    Missing,
    SupportedOriginal,
    AcceptedPatched,
    Unknown
}

public sealed record GameInstallation(string RootPath, string? SteamBuildId)
{
    public string AssemblyPath => Path.Combine(RootPath, "BongoCat_Data", "Managed", "Assembly-CSharp.dll");
}

public sealed record InstallationStatus(
    GameInstallation? Installation,
    AssemblyState AssemblyState,
    string? Sha256,
    CompatibilityDefinition? Compatibility,
    string Message);

public sealed record OperationResult(bool Success, string Message, string? BackupPath = null);
