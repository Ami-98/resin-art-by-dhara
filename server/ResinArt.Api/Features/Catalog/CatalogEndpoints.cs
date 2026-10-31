using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async (IDocumentStore<CategoryDocument> store, CancellationToken ct) =>
        {
            var items = await store.QueryAsync(ct: ct);
            return Results.Ok(items.OrderBy(c => c.SortOrder).ThenBy(c => c.Name));
        }).WithTags("Catalog");

        app.MapGet("/api/products", async (
            string? region,
            string? category,
            string? type,
            bool? featured,
            string? q,
            IDocumentStore<ProductDocument> store,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            IOptionsMonitor<CatalogOptions> catalogOptions,
            CancellationToken ct) =>
        {
            var regionCode = regions.Normalize(region);
            if (regionCode is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["region"] = [$"Unsupported region '{region}'."]
                });

            ProductType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!Enum.TryParse<ProductType>(type.Trim(), ignoreCase: true, out var pt))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["type"] = [$"Unsupported product type '{type}'."]
                    });
                }
                parsedType = pt;
            }

            var regDef = regions.Find(regionCode)!;
            var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            var catFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

            var filteredTask = store.QueryAsync(p =>
                p.Status == ProductStatus.Published &&
                (catFilter is null || string.Equals(p.Category, catFilter, StringComparison.OrdinalIgnoreCase)) &&
                (!parsedType.HasValue || p.Type == parsedType.Value) &&
                (featured != true || p.IsFeatured) &&
                (term is null ||
                 p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.ShortDescription.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase))), ct);

            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(filteredTask, rulesTask);

            var rules = await rulesTask;
            var taxLabel = rules.Tax.TryGetValue(regionCode, out var tax) ? tax.Label : "";
            var placeholderImg = catalogOptions.CurrentValue.PlaceholderImageUrl;

            var cards = (await filteredTask)
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.Name)
                .Select(p => ToSummaryDto(p, regDef, taxLabel, placeholderImg))
                .ToList();

            return Results.Ok(new ProductListResponse(regionCode, regDef.Currency, cards));
        }).WithTags("Catalog");

        app.MapGet("/api/products/{slug}", async (
            string slug,
            string? region,
            IDocumentStore<ProductDocument> store,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            PricingEngine pricingEngine,
            CancellationToken ct) =>
        {
            var regionCode = regions.Normalize(region);
            if (regionCode is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["region"] = [$"Unsupported region '{region}'."]
                });

            var regDef = regions.Find(regionCode)!;
            var productTask = store.FindByIdOrSlugAsync(slug, publishedOnly: true, ct);
            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(productTask, rulesTask);

            var product = await productTask;
            if (product is null)
                return Results.NotFound(new { message = $"Product '{slug}' not found." });

            var rules = await rulesTask;
            var defaultQuote = pricingEngine.CalculateQuote(product, rules, regionCode, selections: null, extraIds: null);

            return Results.Ok(ToDetailDto(product, regDef, rules, defaultQuote));
        }).WithTags("Catalog");

        return app;
    }

    private static ProductSummaryDto ToSummaryDto(
        ProductDocument p,
        RegionDefinition region,
        string taxLabel,
        string placeholderImageUrl)
    {
        var primary = p.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault();
        var price = p.BasePrices.TryGetValue(region.Code, out var bp)
            ? new MoneyDto(bp.Amount, region.Currency)
            : new MoneyDto(0, region.Currency);

        return new ProductSummaryDto(
            p.Id,
            p.Slug,
            p.Name,
            p.Category,
            p.Type,
            p.ShortDescription,
            primary?.Url ?? placeholderImageUrl,
            primary?.Alt ?? p.Name,
            price,
            TaxLabel: taxLabel,
            HasCustomOptions: p.Options.Count > 0,
            p.LeadTimeDays,
            p.Tags,
            p.IsFeatured);
    }

    private static ProductDetailDto ToDetailDto(
        ProductDocument p,
        RegionDefinition region,
        PricingRulesDocument rules,
        QuoteResult initialQuote)
    {
        var baseAmount = p.BasePrices.TryGetValue(region.Code, out var bp) ? bp.Amount : 0L;
        var options = p.Options.Select(o => new ProductOptionDto(
            o.Id,
            o.Label,
            o.Type,
            o.Required,
            o.HelpText,
            o.Constraints,
            o.Choices.Select(c => new OptionChoiceDto(
                c.Id,
                c.Label,
                ToRegionalModifier(c.Modifier, region))).ToList(),
            ToRegionalModifier(o.Modifier, region))).ToList();

        var extras = rules.Extras.Select(e => new RegionalExtraDto(
            e.Id,
            e.Label,
            ToRegionalModifier(e.Modifier, region)!)).ToList();

        var shipping = rules.Shipping.TryGetValue(region.Code, out var s) ? s : new ShippingRule();
        var tax = rules.Tax.TryGetValue(region.Code, out var t) ? t : new TaxRule();

        return new ProductDetailDto(
            p.Id,
            p.Slug,
            p.Name,
            p.Category,
            p.Type,
            p.ShortDescription,
            p.Description,
            p.Images.OrderBy(i => i.SortOrder).ToList(),
            Region: region.Code,
            Currency: region.Currency,
            BasePrice: new MoneyDto(baseAmount, region.Currency),
            InitialQuote: initialQuote,
            Options: options,
            Extras: extras,
            Shipping: shipping,
            Tax: tax,
            p.LeadTimeDays,
            p.Tags,
            p.IsFeatured);
    }

    private static RegionalModifierDto? ToRegionalModifier(PriceModifier? mod, RegionDefinition region)
    {
        if (mod is null) return null;
        return mod.Kind switch
        {
            ModifierKind.Fixed => new RegionalModifierDto(ModifierKind.Fixed, mod.GetFixedForRegion(region.Code), region.Currency, null),
            ModifierKind.Percent => new RegionalModifierDto(ModifierKind.Percent, null, null, mod.Value),
            _ => null
        };
    }
}

public sealed record ProductListResponse(string Region, string Currency, IReadOnlyList<ProductSummaryDto> Items);

public sealed record ProductSummaryDto(
    string Id,
    string Slug,
    string Name,
    string Category,
    ProductType Type,
    string ShortDescription,
    string PrimaryImageUrl,
    string PrimaryImageAlt,
    MoneyDto FromPrice,
    string TaxLabel,
    bool HasCustomOptions,
    int LeadTimeDays,
    List<string> Tags,
    bool IsFeatured);

public sealed record RegionalModifierDto(
    ModifierKind Kind,
    long? Amount,
    string? Currency,
    decimal? Percent);

public sealed record OptionChoiceDto(string Id, string Label, RegionalModifierDto? Modifier);

public sealed record ProductOptionDto(
    string Id,
    string Label,
    OptionType Type,
    bool Required,
    string? HelpText,
    OptionConstraints? Constraints,
    List<OptionChoiceDto> Choices,
    RegionalModifierDto? Modifier);

public sealed record RegionalExtraDto(string Id, string Label, RegionalModifierDto Modifier);

public sealed record ProductDetailDto(
    string Id,
    string Slug,
    string Name,
    string Category,
    ProductType Type,
    string ShortDescription,
    string Description,
    List<ProductImage> Images,
    string Region,
    string Currency,
    MoneyDto BasePrice,
    QuoteResult InitialQuote,
    List<ProductOptionDto> Options,
    List<RegionalExtraDto> Extras,
    ShippingRule Shipping,
    TaxRule Tax,
    int LeadTimeDays,
    List<string> Tags,
    bool IsFeatured);
