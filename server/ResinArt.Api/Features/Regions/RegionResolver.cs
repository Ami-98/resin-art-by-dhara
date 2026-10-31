using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace ResinArt.Api.Features.Regions;

/// <summary>
/// Owns all region rules: supported regions, country mapping and coordinate matching.
/// Caches an immutable O(1) lookup snapshot that refreshes automatically when <see cref="IOptionsMonitor{TOptions}"/> reloads.
/// </summary>
public sealed class RegionResolver : IDisposable
{
    private readonly IDisposable? _changeSubscription;
    private volatile LookupSnapshot _snapshot;

    public RegionResolver(IOptionsMonitor<RegionOptions> options)
    {
        _snapshot = BuildSnapshot(options.CurrentValue);
        _changeSubscription = options.OnChange(updated => _snapshot = BuildSnapshot(updated));
    }

    public IReadOnlyList<RegionDefinition> All => _snapshot.Options.Definitions;

    public string DefaultRegion => _snapshot.Options.Default;

    public RegionDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return _snapshot.ByCode.TryGetValue(code.Trim(), out var def) ? def : null;
    }

    public bool IsSupported(string? code) => Find(code) is not null;

    /// <summary>Returns the canonical region code, or the default region when <paramref name="code"/> is empty.</summary>
    public string? Normalize(string? code)
        => string.IsNullOrWhiteSpace(code) ? _snapshot.Options.Default : Find(code)?.Code;

    /// <summary>Maps an ISO country code to a region. Unknown countries map to the configured fallback; null input → null.</summary>
    public string? FromCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        var snap = _snapshot;
        return snap.CountryMap.TryGetValue(countryCode.Trim(), out var regionCode) && snap.ByCode.TryGetValue(regionCode, out var def)
            ? def.Code
            : snap.Options.FallbackForUnsupported;
    }

    /// <summary>Matches coordinates against the ordered geo rules.</summary>
    public RegionMatch FromCoordinates(double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(lat), "Coordinates are out of range.");

        var snap = _snapshot;
        var rule = snap.Options.GeoRules.FirstOrDefault(r => r.Contains(lat, lng));
        return rule is null || !snap.ByCode.TryGetValue(rule.Region, out var def)
            ? new RegionMatch(snap.Options.FallbackForUnsupported, Matched: false, Label: null)
            : new RegionMatch(def.Code, Matched: true, Label: rule.Label);
    }

    private static LookupSnapshot BuildSnapshot(RegionOptions opts)
    {
        var byCode = opts.Definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var countryMap = opts.CountryMap
            .ToFrozenDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        return new LookupSnapshot(opts, byCode, countryMap);
    }

    public void Dispose() => _changeSubscription?.Dispose();

    private sealed record LookupSnapshot(
        RegionOptions Options,
        FrozenDictionary<string, RegionDefinition> ByCode,
        FrozenDictionary<string, string> CountryMap);
}

public sealed record RegionMatch(string RegionCode, bool Matched, string? Label);
