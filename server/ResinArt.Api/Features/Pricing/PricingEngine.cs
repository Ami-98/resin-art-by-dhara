using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Regions;

namespace ResinArt.Api.Features.Pricing;

/// <summary>
/// Deterministic, server-side pricing engine used by both public quote endpoints and admin preview.
/// Order of operations:
/// 1) Base price for region
/// 2) + Fixed option / choice modifiers for region
/// 3) + Fixed global extras for region
/// 4) + Percent option modifiers &amp; percent extras (applied to the running subtotal)
/// 5) Regional rounding (nearest step or charm ending)
/// 6) + Shipping (when not included)
/// </summary>
public sealed class PricingEngine(RegionResolver regions)
{
    public QuoteResult CalculateQuote(
        ProductDocument product,
        PricingRulesDocument rules,
        string regionCode,
        IReadOnlyList<OptionSelection>? selections,
        IReadOnlyList<string>? extraIds)
    {
        var region = regions.Find(regionCode)
            ?? throw new ArgumentException($"Unsupported region '{regionCode}'.", nameof(regionCode));

        if (!product.BasePrices.TryGetValue(region.Code, out var basePrice))
            throw new InvalidOperationException($"Product '{product.Slug}' has no base price for region '{region.Code}'.");

        var currency = region.Currency;
        var lines = new List<QuoteLine>
        {
            new("Base price", basePrice.Amount)
        };

        long subtotal = basePrice.Amount;
        var percentAddons = new List<(string Label, decimal Percent)>();
        var selectionMap = (selections ?? []).ToDictionary(s => s.OptionId, StringComparer.OrdinalIgnoreCase);

        foreach (var opt in product.Options)
        {
            selectionMap.TryGetValue(opt.Id, out var sel);

            switch (opt.Type)
            {
                case OptionType.Select:
                {
                    var choice = sel?.ChoiceId is not null
                        ? opt.Choices.FirstOrDefault(c => string.Equals(c.Id, sel.ChoiceId, StringComparison.OrdinalIgnoreCase))
                        : opt.Choices.FirstOrDefault();

                    if (choice?.Modifier is { } mod)
                        ApplyModifier($"{opt.Label}: {choice.Label}", mod, region.Code, ref subtotal, lines, percentAddons);
                    break;
                }
                case OptionType.Text:
                {
                    if (!string.IsNullOrWhiteSpace(sel?.Value) && opt.Modifier is { } mod)
                        ApplyModifier(opt.Label, mod, region.Code, ref subtotal, lines, percentAddons);
                    break;
                }
                case OptionType.Image:
                {
                    var hasImage = !string.IsNullOrWhiteSpace(sel?.Value) || sel?.Provided == true;
                    if (hasImage && opt.Modifier is { } mod)
                        ApplyModifier(opt.Label, mod, region.Code, ref subtotal, lines, percentAddons);
                    break;
                }
                case OptionType.Boolean:
                {
                    var enabled = sel?.Provided == true
                        || string.Equals(sel?.Value, "true", StringComparison.OrdinalIgnoreCase);
                    if (enabled && opt.Modifier is { } mod)
                        ApplyModifier(opt.Label, mod, region.Code, ref subtotal, lines, percentAddons);
                    break;
                }
            }
        }

        if (extraIds is { Count: > 0 })
        {
            var requested = extraIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in rules.Extras.Where(e => requested.Contains(e.Id)))
            {
                ApplyModifier(extra.Label, extra.Modifier, region.Code, ref subtotal, lines, percentAddons);
            }
        }

        var percentBase = subtotal;
        foreach (var (label, pct) in percentAddons)
        {
            var delta = (long)Math.Round(percentBase * (pct / 100m), MidpointRounding.AwayFromZero);
            if (delta != 0)
            {
                subtotal += delta;
                lines.Add(new QuoteLine($"{label} (+{pct:0.##}%)", delta));
            }
        }

        if (rules.RegionRules.TryGetValue(region.Code, out var regRule))
        {
            var rounded = ApplyRounding(subtotal, regRule.Rounding);
            if (rounded != subtotal)
            {
                lines.Add(new QuoteLine("Regional rounding", rounded - subtotal));
                subtotal = rounded;
            }
        }

        long shippingAmount = 0;
        string shippingNote = "";
        if (rules.Shipping.TryGetValue(region.Code, out var ship))
        {
            shippingNote = ship.Note;
            shippingAmount = ship.Mode switch
            {
                ShippingMode.Included => 0,
                ShippingMode.Flat => ship.Amount,
                ShippingMode.FreeAbove => ship.FreeAbove.HasValue && subtotal >= ship.FreeAbove.Value ? 0 : ship.Amount,
                _ => 0
            };
            if (shippingAmount > 0)
                lines.Add(new QuoteLine("Shipping", shippingAmount));
        }

        var taxLabel = rules.Tax.TryGetValue(region.Code, out var tax) ? tax.Label : "";

        return new QuoteResult(
            ProductId: product.Id,
            Region: region.Code,
            Currency: currency,
            Total: subtotal + shippingAmount,
            Lines: lines,
            ShippingNote: shippingNote,
            TaxLabel: taxLabel,
            IsEstimate: true);
    }

    private static void ApplyModifier(
        string label,
        PriceModifier mod,
        string region,
        ref long subtotal,
        List<QuoteLine> lines,
        List<(string Label, decimal Percent)> percentAddons)
    {
        if (mod.Kind == ModifierKind.Fixed)
        {
            var amt = mod.GetFixedForRegion(region);
            if (amt != 0)
            {
                subtotal += amt;
                lines.Add(new QuoteLine(label, amt));
            }
        }
        else if (mod.Kind == ModifierKind.Percent && mod.Value != 0)
        {
            percentAddons.Add((label, mod.Value));
        }
    }

    public static long ApplyRounding(long amountMinor, RoundingRule rule)
    {
        if (amountMinor <= 0) return amountMinor;
        return rule.Mode switch
        {
            RoundingMode.Nearest when rule.Step > 0
                => (long)Math.Round((decimal)amountMinor / rule.Step, MidpointRounding.AwayFromZero) * rule.Step,
            RoundingMode.Charm => ApplyCharm(amountMinor, rule.Ending),
            _ => amountMinor
        };
    }

    private static long ApplyCharm(long amountMinor, int ending)
    {
        // Round to nearest major unit (100 minor units) then apply psychological ending (e.g. .99 -> -1 minor unit from next hundred)
        var major = (long)Math.Round(amountMinor / 100m, MidpointRounding.AwayFromZero);
        if (major < 1) major = 1;
        return ending == 99
            ? (major * 100) - 1
            : ((major - 1) * 100) + Math.Clamp(ending, 0, 99);
    }

    /// <summary>Derives a regional amount in minor units from a base-region amount using rate, markup and rounding.</summary>
    public static long DeriveAmount(long baseAmountMinor, RegionDerivationRule rule)
    {
        if (rule.Rate <= 0) throw new ArgumentOutOfRangeException(nameof(rule), "Rate must be positive.");
        var raw = (baseAmountMinor / rule.Rate) * (1m + (rule.MarkupPercent / 100m));
        var minor = (long)Math.Round(raw, MidpointRounding.AwayFromZero);
        return ApplyRounding(minor, rule.Rounding);
    }
}

public sealed record OptionSelection(string OptionId, string? ChoiceId = null, string? Value = null, bool? Provided = null);
public sealed record QuoteLine(string Label, long Amount);
public sealed record QuoteResult(
    string ProductId,
    string Region,
    string Currency,
    long Total,
    IReadOnlyList<QuoteLine> Lines,
    string ShippingNote,
    string TaxLabel,
    bool IsEstimate);
