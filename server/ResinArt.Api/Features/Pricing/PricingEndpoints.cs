using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Pricing;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/pricing/quote", async (
            QuoteRequest req,
            IDocumentStore<ProductDocument> products,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            PricingEngine engine,
            CancellationToken ct) =>
        {
            var regionCode = regions.Normalize(req.Region);
            if (regionCode is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["region"] = [$"Unsupported region '{req.Region}'."]
                });

            var productTask = products.FindByIdOrSlugAsync(req.ProductId, publishedOnly: true, ct);
            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(productTask, rulesTask);

            var product = await productTask;
            if (product is null)
                return Results.NotFound(new { message = "Product not found." });

            var rules = await rulesTask;
            var quote = engine.CalculateQuote(product, rules, regionCode, req.Selections, req.Extras);
            return Results.Ok(quote);
        }).WithTags("Pricing");

        return app;
    }
}

public sealed record QuoteRequest(
    string ProductId,
    string? Region,
    List<OptionSelection>? Selections,
    List<string>? Extras);
