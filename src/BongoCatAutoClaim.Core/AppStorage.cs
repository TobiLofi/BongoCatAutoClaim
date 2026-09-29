namespace BongoCatAutoClaim.Core;

public sealed class AppStorage
{
    public AppStorage(string? basePath = null)
    {
        BasePath = Path.GetFullPath(basePath ?? AppContext.BaseDirectory);
        BackupPath = Path.Combine(BasePath, "Backups");
        LogPath = Path.Combine(BasePath, "Logs");
    }

    public string BasePath { get; }
    public string BackupPath { get; }
    public string LogPath { get; }

    public string BackupFile(CompatibilityDefinition definition) => Path.Combine(
        BackupPath,
        definition.SteamBuildId,
        $"Assembly-CSharp.{definition.OriginalSha256}.original.dll");
}

public sealed class DiagnosticLog
{
    private readonly string logFile;
    private readonly object gate = new();

    public DiagnosticLog(AppStorage storage)
    {
        Directory.CreateDirectory(storage.LogPath);
        logFile = Path.Combine(storage.LogPath, "BongoCatAutoClaim.log");
    }

    public void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
        lock (gate)
            File.AppendAllText(logFile, line);
    }
}
