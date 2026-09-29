namespace BongoCatAutoClaim.Core;

public sealed class BackupManager(AppStorage storage)
{
    public string EnsurePristineBackup(string sourceAssembly, CompatibilityDefinition definition)
    {
        if (!FileHash.Sha256(sourceAssembly).Equals(definition.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The source assembly is not the validated original and cannot become a pristine backup.");

        var backup = storage.BackupFile(definition);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        if (File.Exists(backup))
        {
            VerifyBackup(backup, definition);
            return backup;
        }

        var temporary = backup + ".tmp";
        try
        {
            File.Copy(sourceAssembly, temporary, overwrite: false);
            VerifyBackup(temporary, definition);
            File.Move(temporary, backup);
            return backup;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public string GetVerifiedBackup(CompatibilityDefinition definition)
    {
        var backup = storage.BackupFile(definition);
        if (!File.Exists(backup))
            throw new FileNotFoundException("No pristine backup is available for this supported build.", backup);
        VerifyBackup(backup, definition);
        return backup;
    }

    private static void VerifyBackup(string path, CompatibilityDefinition definition)
    {
        var hash = FileHash.Sha256(path);
        if (!hash.Equals(definition.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The pristine backup failed SHA-256 verification.");
    }
}
