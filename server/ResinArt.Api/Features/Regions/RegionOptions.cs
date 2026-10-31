using System.ComponentModel.DataAnnotations;

namespace ResinArt.Api.Features.Regions;

/// <summary>"Regions" configuration section.</summary>
public sealed class RegionOptions : IValidatableObject
{
    public const string Section = "Regions";

    [Required]
    public string Default { get; set; } = "GJ-IN";

    /// <summary>Region used for visitors from countries we do not explicitly support.</summary>
    [Required]
    public string FallbackForUnsupported { get; set; } = "US";

    /// <summary>ISO-3166 alpha-2 country code → region code.</summary>
    public Dictionary<string, string> CountryMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<RegionDefinition> Definitions { get; set; } = [];

    /// <summary>
    /// Ordered coordinate rules for the opt-in "use my precise location" feature. First match wins, so more
    /// specific boxes (e.g. Gujarat, southern Canadian cities) must come before broad ones (India, contiguous US).
    /// Boxes are approximations; the visitor can always change region manually.
    /// </summary>
    public List<GeoRule> GeoRules { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var codes = Definitions.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
            yield return new ValidationResult("At least one region must be defined.", [nameof(Definitions)]);
        if (!codes.Contains(Default))
            yield return new ValidationResult($"Default region '{Default}' is not defined.", [nameof(Default)]);
        if (!codes.Contains(FallbackForUnsupported))
            yield return new ValidationResult($"Fallback region '{FallbackForUnsupported}' is not defined.", [nameof(FallbackForUnsupported)]);
        foreach (var (country, region) in CountryMap)
            if (!codes.Contains(region))
                yield return new ValidationResult($"CountryMap '{country}' points to unknown region '{region}'.", [nameof(CountryMap)]);
        foreach (var rule in GeoRules)
            if (!codes.Contains(rule.Region))
                yield return new ValidationResult($"GeoRule '{rule.Label}' points to unknown region '{rule.Region}'.", [nameof(GeoRules)]);
    }
}

public sealed class RegionDefinition
{
    [Required] public string Code { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    [Required] public string CountryCode { get; set; } = "";
    [Required] public string Currency { get; set; } = "";
    [Required] public string Locale { get; set; } = "";
    public string? Flag { get; set; }

    /// <summary>IANA time zones used by the client as a last-resort detection heuristic.</summary>
    public List<string> Timezones { get; set; } = [];
}

public sealed class GeoRule
{
    public string Label { get; set; } = "";
    public string Region { get; set; } = "";
    public double MinLat { get; set; }
    public double MaxLat { get; set; }
    public double MinLng { get; set; }
    public double MaxLng { get; set; }

    public bool Contains(double lat, double lng)
        => lat >= MinLat && lat <= MaxLat && lng >= MinLng && lng <= MaxLng;
}

/// <summary>"GeoIp" section – IP → country lookup used for first-visit region suggestion.</summary>
public sealed class GeoIpOptions
{
    public const string Section = "GeoIp";

    /// <summary>Trust Cloudflare's CF-IPCountry header. Only enable when the app is reachable exclusively through Cloudflare.</summary>
    public bool UseCloudflareHeader { get; set; } = true;

    /// <summary>Optional MaxMind GeoLite2-Country database, relative to the data root.</summary>
    public string? FallbackDatabase { get; set; } = "geo/GeoLite2-Country.mmdb";

    /// <summary>Development aid: pretend every visitor comes from this country (e.g. "US").</summary>
    public string? DevCountryOverride { get; set; }
}
