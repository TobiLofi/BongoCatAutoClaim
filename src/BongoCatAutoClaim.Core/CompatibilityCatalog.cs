using System.Reflection;
using System.Text.Json;

namespace BongoCatAutoClaim.Core;

public sealed class CompatibilityCatalog
{
    private readonly IReadOnlyList<CompatibilityDefinition> definitions;

    private CompatibilityCatalog(IReadOnlyList<CompatibilityDefinition> definitions) => this.definitions = definitions;

    public IReadOnlyList<CompatibilityDefinition> Definitions => definitions;

    public static CompatibilityCatalog LoadEmbedded()
    {
        var assembly = typeof(CompatibilityCatalog).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var loaded = new List<CompatibilityDefinition>();
        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidDataException($"Embedded compatibility resource is missing: {resource}");
            var dto = JsonSerializer.Deserialize<CompatibilityDto>(stream, JsonOptions)
                ?? throw new InvalidDataException($"Compatibility resource is empty: {resource}");
            loaded.Add(dto.ToDefinition());
        }

        if (loaded.Count == 0)
            throw new InvalidDataException("No embedded compatibility definitions were found.");

        return new CompatibilityCatalog(loaded);
    }

    public CompatibilityDefinition? FindByHash(string sha256) => definitions.FirstOrDefault(
        definition => definition.OriginalSha256.Equals(sha256, StringComparison.OrdinalIgnoreCase)
            || definition.AcceptedPatchedSha256.Equals(sha256, StringComparison.OrdinalIgnoreCase));

    public CompatibilityDefinition? FindByBuild(string? buildId) => buildId is null
        ? null
        : definitions.FirstOrDefault(definition => definition.SteamBuildId == buildId);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record CompatibilityDto(
        string SteamAppId,
        string SteamBuildId,
        string DisplayName,
        string OriginalSha256,
        string AcceptedPatchedSha256,
        string ModuleMvid,
        string PatchDefinition,
        int StockRefreshSeconds,
        string RuntimeAcceptance)
    {
        public CompatibilityDefinition ToDefinition()
        {
            if (!Guid.TryParse(ModuleMvid, out var mvid))
                throw new InvalidDataException($"Invalid module MVID in compatibility definition: {ModuleMvid}");
            if (OriginalSha256.Length != 64 || AcceptedPatchedSha256.Length != 64)
                throw new InvalidDataException("Compatibility SHA-256 values must contain 64 hexadecimal characters.");
            return new CompatibilityDefinition(SteamAppId, SteamBuildId, DisplayName, OriginalSha256.ToUpperInvariant(),
                AcceptedPatchedSha256.ToUpperInvariant(), mvid, PatchDefinition, StockRefreshSeconds, RuntimeAcceptance);
        }
    }
}
