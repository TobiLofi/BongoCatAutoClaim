using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BongoCatAutoClaim.Core;

public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static SemanticVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a valid stable semantic version.");

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text.StartsWith('v')) text = text[1..];
        var parts = text.Split('.');
        if (parts.Length != 3 ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
            return false;

        version = new SemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;
        comparison = Minor.CompareTo(other.Minor);
        return comparison != 0 ? comparison : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    private static bool TryParsePart(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
}

public static class UtilityVersion
{
    public static SemanticVersion Current { get; } = FromAssembly(typeof(UtilityVersion).Assembly);
    public static string Display => $"v{Current}";

    private static SemanticVersion FromAssembly(Assembly assembly)
    {
        var version = assembly.GetName().Version
            ?? throw new InvalidOperationException("The utility assembly version is unavailable.");
        return new SemanticVersion(version.Major, version.Minor, Math.Max(version.Build, 0));
    }
}

public interface IReleaseFeedClient
{
    Task<string> GetPublishedReleasesJsonAsync(CancellationToken cancellationToken = default);
}

public sealed class GitHubReleaseFeedClient(HttpClient httpClient) : IReleaseFeedClient
{
    public static readonly Uri ReleasesEndpoint =
        new("https://api.github.com/repos/TobiLofi/BongoCatAutoClaim/releases?per_page=100");

    public async Task<string> GetPublishedReleasesJsonAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BongoCatAutoClaim", UtilityVersion.Current.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}

public enum UpdateCheckState
{
    UpToDate,
    UpdateAvailable,
    NoStableRelease
}

public sealed record UpdateCheckResult(UpdateCheckState State, SemanticVersion CurrentVersion,
    SemanticVersion? LatestVersion = null, Uri? ReleasePage = null);

public sealed class UpdateChecker(IReleaseFeedClient releaseFeedClient)
{
    public async Task<UpdateCheckResult> CheckAsync(SemanticVersion currentVersion,
        CancellationToken cancellationToken = default)
    {
        var json = await releaseFeedClient.GetPublishedReleasesJsonAsync(cancellationToken).ConfigureAwait(false);
        List<GitHubRelease>? releases;
        try
        {
            releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("GitHub returned malformed release data.", exception);
        }

        if (releases is null) throw new InvalidDataException("GitHub returned malformed release data.");
        var stableReleases = releases.Where(release => !release.Draft && !release.Prerelease).ToArray();
        if (stableReleases.Length == 0)
            return new UpdateCheckResult(UpdateCheckState.NoStableRelease, currentVersion);

        var parsedVersions = stableReleases
            .Select(release => SemanticVersion.TryParse(release.TagName, out var version) ? version : (SemanticVersion?)null)
            .ToArray();
        if (parsedVersions.Any(version => version is null))
            throw new InvalidDataException("GitHub returned a stable release with an invalid version tag.");

        var latest = parsedVersions.Select(version => version!.Value).Max();
        return latest.CompareTo(currentVersion) > 0
            ? new UpdateCheckResult(UpdateCheckState.UpdateAvailable, currentVersion, latest,
                OfficialReleasePage.Create(latest))
            : new UpdateCheckResult(UpdateCheckState.UpToDate, currentVersion, latest);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }
}

public static class OfficialReleasePage
{
    private const string RepositoryPath = "/TobiLofi/BongoCatAutoClaim";

    public static Uri Create(SemanticVersion version) =>
        new($"https://github.com{RepositoryPath}/releases/tag/v{version}");

    public static bool IsOfficial(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        const string prefix = RepositoryPath + "/releases/tag/";
        return uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) &&
               SemanticVersion.TryParse(Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]), out _);
    }
}
