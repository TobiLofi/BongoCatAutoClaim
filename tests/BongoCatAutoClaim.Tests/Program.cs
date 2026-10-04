using BongoCatAutoClaim.Core;

var catalog = CompatibilityCatalog.LoadEmbedded();
Assert(catalog.Definitions.Count == 2, "Exactly two audited compatibility definitions should be present.");
Assert(catalog.Definitions.Select(definition => definition.SteamBuildId).OrderBy(id => id).SequenceEqual(new[] { "25562987", "25659569" }),
    "Supported Steam build IDs mismatch.");
foreach (var definition in catalog.Definitions)
{
    Assert(definition.SteamAppId == "3419430", "Steam app ID mismatch.");
    Assert(definition.StockRefreshSeconds == 1800, "The normal 30-minute cooldown must remain unchanged.");
    Assert(catalog.FindByHash(definition.OriginalSha256) == definition, "Original hash lookup failed.");
    Assert(catalog.FindByHash(definition.AcceptedPatchedSha256) == definition, "Patched hash lookup failed.");
}
Assert(catalog.FindByHash(new string('0', 64)) is null, "Unknown hashes must be rejected.");

var assemblyOption = Array.FindIndex(args, item => item.Equals("--assembly", StringComparison.OrdinalIgnoreCase));
if (assemblyOption >= 0)
{
    if (assemblyOption + 1 >= args.Length)
        throw new ArgumentException("--assembly requires a path to a locally owned pristine Assembly-CSharp.dll.");
    var source = Path.GetFullPath(args[assemblyOption + 1]);
    Assert(File.Exists(source), "Local validation assembly does not exist.");
    var definition = catalog.FindByHash(FileHash.Sha256(source))
        ?? throw new InvalidOperationException("Local validation assembly is not a supported pristine original.");
    Assert(FileHash.Sha256(source) == definition.OriginalSha256, "Local validation assembly is not the supported pristine original.");

    var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"BongoCatAutoClaim.Tests.{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        var output = Path.Combine(temporaryDirectory, "Assembly-CSharp.dll");
        var engine = new PatchEngine();
        engine.VerifyOriginalAssembly(source, definition);
        engine.CreatePatchedAssembly(source, output, definition);
        engine.VerifyPatchedAssembly(output, definition);
        Assert(FileHash.Sha256(output) == definition.AcceptedPatchedSha256, "Generated output does not match the runtime-accepted hash.");
        Console.WriteLine("Local compatibility fixture reproduced the accepted patched hash.");

        var mockGameRoot = Path.Combine(temporaryDirectory, "MockGame");
        var mockManaged = Path.Combine(mockGameRoot, "BongoCat_Data", "Managed");
        Directory.CreateDirectory(mockManaged);
        var mockAssembly = Path.Combine(mockManaged, "Assembly-CSharp.dll");
        File.Copy(source, mockAssembly);
        var storage = new AppStorage(Path.Combine(temporaryDirectory, "PortableApp"));
        var manager = new AutoClaimManager(catalog, storage, isGameRunning: () => false);
        var installation = new GameInstallation(mockGameRoot, definition.SteamBuildId);

        var initialStatus = manager.GetStatus(installation);
        Assert(initialStatus.AssemblyState == AssemblyState.SupportedOriginal, "Mock installation should begin in the supported-original state.");
        var installResult = manager.Install(installation);
        Assert(installResult.Success, "Isolated install operation failed.");
        Assert(FileHash.Sha256(mockAssembly) == definition.AcceptedPatchedSha256, "Isolated install did not produce the accepted patched hash.");
        var backupPath = installResult.BackupPath ?? throw new InvalidOperationException("Isolated install did not report its pristine backup.");
        Assert(File.Exists(backupPath), "Isolated install did not create its pristine backup.");
        Assert(FileHash.Sha256(backupPath) == definition.OriginalSha256, "Isolated pristine backup hash mismatch.");

        var restoreResult = manager.Restore(installation);
        Assert(restoreResult.Success, "Isolated restore operation failed.");
        Assert(FileHash.Sha256(mockAssembly) == definition.OriginalSha256, "Isolated restore did not return the original hash.");
        Console.WriteLine("Isolated backup, install, verification, and restore workflow passed.");
    }
    finally
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}

if (args.Contains("--detect-installed", StringComparer.OrdinalIgnoreCase))
{
    var detected = new GameDetector().Detect();
    Assert(detected.Count > 0, "Steam detection did not find a Bongo Cat installation.");
    var readOnlyStorage = new AppStorage(Path.Combine(Path.GetTempPath(), $"BongoCatAutoClaim.Status.{Guid.NewGuid():N}"));
    try
    {
        var manager = new AutoClaimManager(catalog, readOnlyStorage, isGameRunning: () => true);
        var statuses = detected.Select(manager.GetStatus).ToArray();
        Assert(statuses.Any(status => status.AssemblyState == AssemblyState.AcceptedPatched),
            "No detected installation matched the accepted patched state.");
        Console.WriteLine("Steam detection recognized the installed runtime-accepted patch without modifying it.");
    }
    finally
    {
        if (Directory.Exists(readOnlyStorage.BasePath))
            Directory.Delete(readOnlyStorage.BasePath, recursive: true);
    }
}

Console.WriteLine("All Bongo Cat Auto Claim checks passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
