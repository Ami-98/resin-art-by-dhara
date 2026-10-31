using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Catalog;

public enum ProductType { Standard, Custom }
public enum ProductStatus { Draft, Published, Archived }
public enum OptionType { Select, Text, Image, Boolean }

public sealed class CategoryDocument : IDocument
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int SortOrder { get; set; }
    public string? ImageUrl { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class ProductDocument : IDocument
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public ProductType Type { get; set; } = ProductType.Standard;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public ProductSeo? Seo { get; set; }
    public List<ProductImage> Images { get; set; } = [];
    public Dictionary<string, RegionalBasePrice> BasePrices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProductOption> Options { get; set; } = [];
    public int LeadTimeDays { get; set; } = 7;
    public List<string> Tags { get; set; } = [];
    public bool IsFeatured { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
}

public sealed class ProductSeo
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public sealed class ProductImage
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string? ThumbUrl { get; set; }
    public string Alt { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public int Width { get; set; } = 1200;
    public int Height { get; set; } = 1200;
}

public sealed class RegionalBasePrice
{
    /// <summary>Minor currency units (paise/cents).</summary>
    public long Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public bool IsOverridden { get; set; } = true;
}

public sealed class ProductOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public OptionType Type { get; set; } = OptionType.Select;
    public bool Required { get; set; }
    public string? HelpText { get; set; }
    public OptionConstraints? Constraints { get; set; }
    public List<OptionChoice> Choices { get; set; } = [];
    public PriceModifier? Modifier { get; set; }
}

public sealed class OptionConstraints
{
    public int? MaxLength { get; set; }
    public string? Placeholder { get; set; }
    public int? MaxFiles { get; set; }
    public int? MaxSizeMb { get; set; }
}

public sealed class OptionChoice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public PriceModifier? Modifier { get; set; }
}

public static class ProductStoreExtensions
{
    /// <summary>
    /// Resolves a product by exact ID (O(1) lookup) or falls back to case-insensitive slug matching.
    /// </summary>
    public static async Task<ProductDocument?> FindByIdOrSlugAsync(
        this IDocumentStore<ProductDocument> store,
        string? idOrSlug,
        bool publishedOnly = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug)) return null;
        var key = idOrSlug.Trim();

        var byId = await store.GetAsync(key, ct);
        if (byId is not null)
            return !publishedOnly || byId.Status == ProductStatus.Published ? byId : null;

        var matches = await store.QueryAsync(p =>
            (!publishedOnly || p.Status == ProductStatus.Published) &&
            (string.Equals(p.Slug, key, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.Id, key, StringComparison.OrdinalIgnoreCase)), ct);

        return matches.FirstOrDefault();
    }
}
