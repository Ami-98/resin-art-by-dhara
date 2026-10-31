using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Regions;

public static class RegionEndpoints
{
    public static IEndpointRouteBuilder MapRegionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/bootstrap", async (
            HttpContext http,
            RegionResolver regions,
            IIpCountryLookup ipLookup,
            IOptionsMonitor<BusinessOptions> business,
            IOptionsMonitor<LeadOptions> leads,
            IOptionsMonitor<FeatureOptions> features,
            IOptionsMonitor<UploadOptions> uploads,
            IOptionsMonitor<CatalogOptions> catalog,
            IDocumentStore<PricingRulesDocument> pricingStore,
            CancellationToken ct) =>
        {
            var country = ipLookup.GetCountry(http);
            var suggested = regions.FromCountry(country);
            var source = suggested is not null ? "ip" : null;

            var pricing = await pricingStore.GetRulesAsync(ct);
            var biz = business.CurrentValue;
            var leadOpts = leads.CurrentValue;
            var uploadOpts = uploads.CurrentValue;
            var catalogOpts = catalog.CurrentValue;

            return Results.Ok(new BootstrapResponse(
                Business: new PublicBusinessInfo(
                    biz.Name,
                    biz.ContactEmail,
                    biz.WhatsApp.BuildUrl(),
                    biz.Social,
                    biz.Tagline,
                    biz.Location,
                    biz.OwnerName,
                    biz.WhatsApp.CustomerReplyTemplate),
                Regions: regions.All,
                DefaultRegion: regions.DefaultRegion,
                SuggestedRegion: suggested,
                SuggestionSource: source,
                DetectedCountry: country,
                Shipping: pricing.Shipping,
                Tax: pricing.Tax,
                Extras: pricing.Extras,
                Features: features.CurrentValue,
                Leads: new PublicLeadSettings(
                    leadOpts.RequireAddress,
                    leadOpts.PrivacyPolicyVersion,
                    leadOpts.RetentionMonths),
                Uploads: new PublicUploadSettings(
                    uploadOpts.MaxFileSizeMb,
                    uploadOpts.MaxFilesPerRequest),
                Catalog: new PublicCatalogSettings(
                    catalogOpts.DefaultCategory,
                    catalogOpts.DefaultLeadTimeDays,
                    catalogOpts.PlaceholderImageUrl)
            ));
        }).WithTags("Regions");

        app.MapGet("/api/regions", (RegionResolver regions) => Results.Ok(regions.All))
            .WithTags("Regions");

        app.MapPost("/api/regions/resolve", (ResolveCoordinatesRequest req, RegionResolver regions) =>
        {
            if (req.Lat is < -90 or > 90 || req.Lng is < -180 or > 180)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["coordinates"] = ["Latitude must be between -90 and 90 and longitude between -180 and 180."]
                });

            var match = regions.FromCoordinates(req.Lat, req.Lng);
            return Results.Ok(match);
        }).WithTags("Regions");

        return app;
    }
}

public sealed record ResolveCoordinatesRequest(double Lat, double Lng);

public sealed record PublicBusinessInfo(
    string Name,
    string? ContactEmail,
    string WhatsAppUrl,
    SocialOptions Social,
    string Tagline = "Gujarat · Handmade Resin Studio",
    string Location = "Gujarat, India",
    string OwnerName = "Dhara",
    string CustomerReplyTemplate = "Hi {name}, this is {owner} from {business} regarding your enquiry ({reference}).");

public sealed record PublicLeadSettings(
    bool RequireAddress,
    string PrivacyPolicyVersion,
    int RetentionMonths = 24);

public sealed record PublicUploadSettings(
    int MaxFileSizeMb,
    int MaxFilesPerRequest);

public sealed record PublicCatalogSettings(
    string DefaultCategory,
    int DefaultLeadTimeDays,
    string PlaceholderImageUrl);

public sealed record BootstrapResponse(
    PublicBusinessInfo Business,
    IReadOnlyList<RegionDefinition> Regions,
    string DefaultRegion,
    string? SuggestedRegion,
    string? SuggestionSource,
    string? DetectedCountry,
    Dictionary<string, ShippingRule> Shipping,
    Dictionary<string, TaxRule> Tax,
    List<PricingExtra> Extras,
    FeatureOptions Features,
    PublicLeadSettings Leads,
    PublicUploadSettings? Uploads = null,
    PublicCatalogSettings? Catalog = null);
