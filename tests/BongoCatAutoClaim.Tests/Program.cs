using BongoCatAutoClaim.Core;

var catalog = CompatibilityCatalog.LoadEmbedded();
Assert(catalog.Definitions.Count == 3, "Exactly three audited compatibility definitions should be present.");
Assert(catalog.Definitions.Select(definition => definition.SteamBuildId).OrderBy(id => id).SequenceEqual(new[] { "25562987", "25659569", "25723683" }),
    "Supported Steam build IDs mismatch.");
foreach (var definition in catalog.Definitions)
{
    Assert(definition.SteamAppId == "3419430", "Steam app ID mismatch.");
    Assert(definition.StockRefreshSeconds == 1800, "The normal 30-minute cooldown must remain unchanged.");
    Assert(catalog.FindByHash(definition.OriginalSha256) == definition, "Original hash lookup failed.");
    Assert(catalog.FindByHash(definition.AcceptedPatchedSha256) == definition, "Patched hash lookup failed.");
}
Assert(catalog.FindByHash(new string('0', 64)) is null, "Unknown hashes must be rejected.");

await VerifyUpdateChecksAsync();

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

static async Task VerifyUpdateChecksAsync()
{
    var current = SemanticVersion.Parse("v1.1.1");

    var same = await Check("""[{"tag_name":"v1.1.1","draft":false,"prerelease":false}]""", current);
    Assert(same.State == UpdateCheckState.UpToDate, "The current release should be reported as up to date.");

    var newer = await Check("""[{"tag_name":"v1.2.0","draft":false,"prerelease":false}]""", current);
    Assert(newer.State == UpdateCheckState.UpdateAvailable && newer.LatestVersion == SemanticVersion.Parse("1.2.0"),
        "A newer stable release should be detected.");
    Assert(newer.ReleasePage == OfficialReleasePage.Create(SemanticVersion.Parse("1.2.0")),
        "The release page must be constructed locally from the validated version.");

    var older = await Check("""[{"tag_name":"v1.0.1","draft":false,"prerelease":false}]""", current);
    Assert(older.State == UpdateCheckState.UpToDate, "An older remote release must not be offered as an update.");

    var decimalOrdering = await Check("""[{"tag_name":"v1.10.0","draft":false,"prerelease":false}]""",
        SemanticVersion.Parse("v1.9.0"));
    Assert(decimalOrdering.State == UpdateCheckState.UpdateAvailable,
        "Semantic comparison must treat v1.10.0 as newer than v1.9.0.");

    await AssertThrowsAsync<InvalidDataException>(() => Check("{not json", current),
        "Malformed GitHub JSON should fail safely.");

    var noReleases = await Check("[]", current);
    Assert(noReleases.State == UpdateCheckState.NoStableRelease, "An empty release list should fail safely.");

    var filtered = await Check("""
        [
          {"tag_name":"v9.0.0","draft":true,"prerelease":false},
          {"tag_name":"v8.0.0","draft":false,"prerelease":true},
          {"tag_name":"v1.1.1","draft":false,"prerelease":false}
        ]
        """, current);
    Assert(filtered.State == UpdateCheckState.UpToDate,
        "Draft and prerelease versions must be ignored when selecting the latest stable release.");

    await AssertThrowsAsync<HttpRequestException>(
        () => new UpdateChecker(new ThrowingReleaseFeed()).CheckAsync(current),
        "HTTP failures should propagate to the GUI's safe error handler.");

    var official = OfficialReleasePage.Create(SemanticVersion.Parse("v2.3.4"));
    Assert(official.AbsoluteUri == "https://github.com/TobiLofi/BongoCatAutoClaim/releases/tag/v2.3.4",
        "Official release URL construction mismatch.");
    Assert(OfficialReleasePage.IsOfficial(official), "A locally constructed official release URL should be accepted.");
    Assert(!OfficialReleasePage.IsOfficial(new Uri("https://example.com/TobiLofi/BongoCatAutoClaim/releases/tag/v2.3.4")),
        "A non-GitHub release URL must be rejected.");
    Assert(!OfficialReleasePage.IsOfficial(new Uri("https://github.com/SomeoneElse/BongoCatAutoClaim/releases/tag/v2.3.4")),
        "A different GitHub owner must be rejected.");

    var handler = new RecordingHandler("[]");
    using var httpClient = new HttpClient(handler);
    var source = new GitHubReleaseFeedClient(httpClient);
    _ = await source.GetPublishedReleasesJsonAsync();
    Assert(handler.RequestMethod == HttpMethod.Get, "The GitHub release request must use GET.");
    Assert(handler.RequestUri == GitHubReleaseFeedClient.ReleasesEndpoint, "The GitHub API endpoint mismatch.");
    Assert(handler.UserAgent?.StartsWith("BongoCatAutoClaim/", StringComparison.Ordinal) == true,
        "The GitHub request must include the utility User-Agent.");
}

static Task<UpdateCheckResult> Check(string json, SemanticVersion current) =>
    new UpdateChecker(new FixedReleaseFeed(json)).CheckAsync(current);

static async Task AssertThrowsAsync<TException>(Func<Task> action, string message) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

file sealed class FixedReleaseFeed(string json) : IReleaseFeedClient
{
    public Task<string> GetPublishedReleasesJsonAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(json);
}

file sealed class ThrowingReleaseFeed : IReleaseFeedClient
{
    public Task<string> GetPublishedReleasesJsonAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<string>(new HttpRequestException("Simulated offline failure."));
}

file sealed class RecordingHandler(string responseBody) : HttpMessageHandler
{
    public HttpMethod? RequestMethod { get; private set; }
    public Uri? RequestUri { get; private set; }
    public string? UserAgent { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestMethod = request.Method;
        RequestUri = request.RequestUri;
        UserAgent = request.Headers.UserAgent.ToString();
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });
    }
}
