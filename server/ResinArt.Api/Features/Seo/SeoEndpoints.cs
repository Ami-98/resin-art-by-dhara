using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Seo;

/// <summary>
/// Serves the SPA shell (<c>wwwroot/index.html</c> or a built-in fallback during development) and injects
/// server-rendered Open Graph, Twitter Card and JSON-LD <c>Product</c> tags for <c>/products/{slug}</c> and
/// <c>/custom/{slug}</c>. This solves C1 from the architecture review: WhatsApp and Instagram link-preview
/// crawlers do not execute JavaScript, so meta tags must be present in the initial HTML response.
/// </summary>
public sealed class SpaMetaService(
    IWebHostEnvironment env,
    IDocumentStore<ProductDocument> products,
    RegionResolver regions,
    IOptionsMonitor<BusinessOptions> business,
    IOptionsMonitor<SeoOptions> seo)
{
    public const string MetaPlaceholder = "<!--RESINART_SEO_META-->";

    private readonly Lock _templateLock = new();
    private string? _cachedTemplate;
    private DateTime _cachedLastWriteUtc;

    public async Task<IResult> RenderIndexAsync(HttpContext http, CancellationToken ct)
    {
        var path = http.Request.Path.Value ?? "/";
        var seoOpts = seo.CurrentValue;
        var origin = GetPublicOrigin(http, seoOpts);
        var canonicalUrl = $"{origin}{path}";
        var siteName = business.CurrentValue.Name;

        ProductDocument? product = null;
        var isProductRoute = TryExtractProductSlug(path, out var slug);
        if (isProductRoute && !string.IsNullOrWhiteSpace(slug))
        {
            product = await products.FindByIdOrSlugAsync(slug, publishedOnly: true, ct);
        }

        var statusCode = isProductRoute && product is null
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status200OK;

        var metaBlock = product is not null
            ? BuildProductMeta(product, siteName, origin, canonicalUrl, regions, seoOpts.PlaceholderImage)
            : BuildSiteMeta(siteName, seoOpts.TitleSuffix, seoOpts.DefaultDescription, origin, canonicalUrl, seoOpts.DefaultImage);

        var template = await LoadShellTemplateAsync(ct);
        var html = template.Contains(MetaPlaceholder, StringComparison.Ordinal)
            ? template.Replace(MetaPlaceholder, metaBlock, StringComparison.Ordinal)
            : InjectBeforeHeadClose(template, metaBlock);

        http.Response.StatusCode = statusCode;
        http.Response.Headers.CacheControl = "no-cache";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static bool TryExtractProductSlug(string path, out string? slug)
    {
        slug = null;
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 2 &&
            (segments[0].Equals("products", StringComparison.OrdinalIgnoreCase) ||
             segments[0].Equals("custom", StringComparison.OrdinalIgnoreCase)))
        {
            slug = Uri.UnescapeDataString(segments[1]);
            return true;
        }
        return false;
    }

    private static string BuildProductMeta(
        ProductDocument product,
        string siteName,
        string origin,
        string canonicalUrl,
        RegionResolver regions,
        string placeholderImage)
    {
        var title = !string.IsNullOrWhiteSpace(product.Seo?.Title)
            ? product.Seo!.Title!
            : $"{product.Name} | {siteName}";
        var description = !string.IsNullOrWhiteSpace(product.Seo?.Description)
            ? product.Seo!.Description!
            : product.ShortDescription;

        var primaryImg = product.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).FirstOrDefault();
        var fallbackImg = string.IsNullOrWhiteSpace(placeholderImage) ? "/uploads/placeholder.jpg" : placeholderImage;
        var imageUrl = ToAbsoluteUrl(origin, primaryImg?.Url ?? fallbackImg);

        var baseRegion = regions.Find(regions.DefaultRegion);
        var currency = baseRegion?.Currency ?? "INR";
        var amountMinor = product.BasePrices.TryGetValue(regions.DefaultRegion, out var bp) ? bp.Amount : 0L;
        var amountMajor = (amountMinor / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        var jsonLd = JsonSerializer.Serialize(new
        {
            @context = "https://schema.org",
            @type = "Product",
            name = product.Name,
            description = product.ShortDescription,
            image = new[] { imageUrl },
            sku = product.Id,
            category = product.Category,
            brand = new { @type = "Brand", name = siteName },
            offers = new
            {
                @type = "Offer",
                url = canonicalUrl,
                priceCurrency = currency,
                price = amountMajor,
                availability = "https://schema.org/MadeToOrder"
            }
        });

        var sb = new StringBuilder();
        sb.AppendLine($"<title>{Enc(title)}</title>");
        sb.AppendLine($"<meta name=\"description\" content=\"{Enc(description)}\" />");
        sb.AppendLine($"<link rel=\"canonical\" href=\"{Enc(canonicalUrl)}\" />");
        sb.AppendLine("<meta property=\"og:type\" content=\"product\" />");
        sb.AppendLine($"<meta property=\"og:site_name\" content=\"{Enc(siteName)}\" />");
        sb.AppendLine($"<meta property=\"og:title\" content=\"{Enc(title)}\" />");
        sb.AppendLine($"<meta property=\"og:description\" content=\"{Enc(description)}\" />");
        sb.AppendLine($"<meta property=\"og:url\" content=\"{Enc(canonicalUrl)}\" />");
        sb.AppendLine($"<meta property=\"og:image\" content=\"{Enc(imageUrl)}\" />");
        sb.AppendLine($"<meta property=\"product:price:amount\" content=\"{Enc(amountMajor)}\" />");
        sb.AppendLine($"<meta property=\"product:price:currency\" content=\"{Enc(currency)}\" />");
        sb.AppendLine("<meta name=\"twitter:card\" content=\"summary_large_image\" />");
        sb.AppendLine($"<meta name=\"twitter:title\" content=\"{Enc(title)}\" />");
        sb.AppendLine($"<meta name=\"twitter:description\" content=\"{Enc(description)}\" />");
        sb.AppendLine($"<meta name=\"twitter:image\" content=\"{Enc(imageUrl)}\" />");
        sb.AppendLine($"<script type=\"application/ld+json\">{jsonLd}</script>");
        return sb.ToString();
    }

    private static string BuildSiteMeta(
        string siteName,
        string titleSuffix,
        string description,
        string origin,
        string canonicalUrl,
        string? defaultImage)
    {
        var fullTitle = string.IsNullOrWhiteSpace(titleSuffix) ? siteName : $"{siteName} — {titleSuffix}";
        var sb = new StringBuilder();
        sb.AppendLine($"<title>{Enc(fullTitle)}</title>");
        sb.AppendLine($"<meta name=\"description\" content=\"{Enc(description)}\" />");
        sb.AppendLine($"<link rel=\"canonical\" href=\"{Enc(canonicalUrl)}\" />");
        sb.AppendLine("<meta property=\"og:type\" content=\"website\" />");
        sb.AppendLine($"<meta property=\"og:site_name\" content=\"{Enc(siteName)}\" />");
        sb.AppendLine($"<meta property=\"og:title\" content=\"{Enc(siteName)}\" />");
        sb.AppendLine($"<meta property=\"og:description\" content=\"{Enc(description)}\" />");
        sb.AppendLine($"<meta property=\"og:url\" content=\"{Enc(canonicalUrl)}\" />");
        if (!string.IsNullOrWhiteSpace(defaultImage))
        {
            var img = ToAbsoluteUrl(origin, defaultImage);
            sb.AppendLine($"<meta property=\"og:image\" content=\"{Enc(img)}\" />");
        }
        sb.AppendLine("<meta name=\"twitter:card\" content=\"summary_large_image\" />");
        return sb.ToString();
    }

    private async Task<string> LoadShellTemplateAsync(CancellationToken ct)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var indexPath = Path.Combine(webRoot, "index.html");
        if (File.Exists(indexPath))
        {
            var lastWriteUtc = File.GetLastWriteTimeUtc(indexPath);
            lock (_templateLock)
            {
                if (_cachedTemplate is not null && _cachedLastWriteUtc == lastWriteUtc)
                    return _cachedTemplate;
            }

            var loaded = await File.ReadAllTextAsync(indexPath, ct);
            lock (_templateLock)
            {
                _cachedTemplate = loaded;
                _cachedLastWriteUtc = lastWriteUtc;
            }
            return loaded;
        }

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              {{MetaPlaceholder}}
            </head>
            <body>
              <div id="root">Resin Art by Dhara API is running. Build the React client or run Vite dev server.</div>
            </body>
            </html>
            """;
    }

    private static string InjectBeforeHeadClose(string html, string metaBlock)
    {
        var idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? html.Insert(idx, metaBlock) : metaBlock + html;
    }

    public static string GetPublicOrigin(HttpContext http, SeoOptions seo)
    {
        if (!string.IsNullOrWhiteSpace(seo.PublicBaseUrl))
            return seo.PublicBaseUrl.TrimEnd('/');
        return $"{http.Request.Scheme}://{http.Request.Host}";
    }

    private static string ToAbsoluteUrl(string origin, string url)
        => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{origin.TrimEnd('/')}/{url.TrimStart('/')}";

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? "");
}

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (HttpContext http, IOptionsMonitor<SeoOptions> seo) =>
        {
            var seoOpts = seo.CurrentValue;
            var origin = SpaMetaService.GetPublicOrigin(http, seoOpts);
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            foreach (var disallow in seoOpts.RobotsDisallowPaths)
            {
                if (!string.IsNullOrWhiteSpace(disallow))
                    sb.AppendLine($"Disallow: {disallow}");
            }
            sb.AppendLine($"Sitemap: {origin}/sitemap.xml");
            return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
        }).ExcludeFromDescription();

        app.MapGet("/sitemap.xml", async (
            HttpContext http,
            IDocumentStore<ProductDocument> products,
            IOptionsMonitor<SeoOptions> seo,
            CancellationToken ct) =>
        {
            var seoOpts = seo.CurrentValue;
            var origin = SpaMetaService.GetPublicOrigin(http, seoOpts);
            var published = await products.QueryAsync(p => p.Status == ProductStatus.Published, ct);

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            foreach (var staticPath in seoOpts.SitemapStaticPaths)
            {
                sb.AppendLine($"  <url><loc>{WebUtility.HtmlEncode(origin + staticPath)}</loc></url>");
            }
            foreach (var p in published)
            {
                var routePrefix = p.Type == ProductType.Custom ? "/custom/" : "/products/";
                var loc = WebUtility.HtmlEncode($"{origin}{routePrefix}{p.Slug}");
                var mod = p.UpdatedAt.ToString("yyyy-MM-dd");
                sb.AppendLine($"  <url><loc>{loc}</loc><lastmod>{mod}</lastmod></url>");
            }
            sb.AppendLine("</urlset>");
            return Results.Text(sb.ToString(), "application/xml; charset=utf-8");
        }).ExcludeFromDescription();

        return app;
    }
}
