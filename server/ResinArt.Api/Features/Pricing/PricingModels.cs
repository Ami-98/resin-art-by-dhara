using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Pricing;

public enum ModifierKind { Fixed, Percent }
public enum RoundingMode { None, Nearest, Charm }
public enum ShippingMode { Included, Flat, FreeAbove }

public sealed class PriceModifier
{
    public ModifierKind Kind { get; set; } = ModifierKind.Fixed;

    /// <summary>Per-region amounts in minor units when <see cref="Kind"/> is <see cref="ModifierKind.Fixed"/>.</summary>
    public Dictionary<string, long> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Percentage (e.g. 10 for +10%) when <see cref="Kind"/> is <see cref="ModifierKind.Percent"/>.</summary>
    public decimal Value { get; set; }

    public bool Derived { get; set; } = true;

    public long GetFixedForRegion(string region)
        => Values.TryGetValue(region, out var v) ? v : 0L;
}

public sealed class PricingRulesDocument : IDocument
{
    public const string SingletonId = "default";

    public string Id { get; set; } = SingletonId;
    public int Version { get; set; } = 1;
    public string BaseRegion { get; set; } = "GJ-IN";
    public Dictionary<string, RegionDerivationRule> RegionRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PricingExtra> Extras { get; set; } = [];
    public Dictionary<string, ShippingRule> Shipping { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TaxRule> Tax { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RegionDerivationRule
{
    /// <summary>Units of base-region currency per 1 unit of target currency (e.g. 83 INR per 1 USD).</summary>
    public decimal Rate { get; set; } = 1m;
    public decimal MarkupPercent { get; set; }
    public RoundingRule Rounding { get; set; } = new();
}

public sealed class RoundingRule
{
    public RoundingMode Mode { get; set; } = RoundingMode.None;
    /// <summary>Step in minor units when Mode == Nearest (e.g. 1000 = nearest ₹10).</summary>
    public long Step { get; set; } = 100;
    /// <summary>Ending in minor units when Mode == Charm (e.g. 99 = $.99).</summary>
    public int Ending { get; set; } = 99;
}

public sealed class PricingExtra
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public PriceModifier Modifier { get; set; } = new();
}

public sealed class ShippingRule
{
    public ShippingMode Mode { get; set; } = ShippingMode.Included;
    public long Amount { get; set; }
    public long? FreeAbove { get; set; }
    public string Note { get; set; } = "";
}

public sealed class TaxRule
{
    public string Display { get; set; } = "inclusive";
    public string Label { get; set; } = "";
    public decimal? RatePercent { get; set; }
}

public sealed record MoneyDto(long Amount, string Currency);

public static class PricingStoreExtensions
{
    public static async Task<PricingRulesDocument> GetRulesAsync(
        this IDocumentStore<PricingRulesDocument> store,
        CancellationToken ct = default)
        => await store.GetAsync(PricingRulesDocument.SingletonId, ct) ?? new PricingRulesDocument();
}
