using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Infrastructure.Images;
using ResinArt.Api.Infrastructure.Settings;

namespace ResinArt.Api.Features.Leads;

public static class LeadEndpoints
{
    public const string LeadRateLimitPolicy = "LeadsPerIp";
    public const string UploadRateLimitPolicy = "UploadsPerIp";

    public static IEndpointRouteBuilder MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contacts", async (
            CreateContactRequest req,
            HttpContext http,
            LeadService leads,
            IOptionsMonitor<LeadOptions> leadOptions,
            IOptionsMonitor<BusinessOptions> businessOptions,
            CancellationToken ct) =>
        {
            var leadOpts = leadOptions.CurrentValue;

            // Honeypot check: bots that fill the hidden "website" field get a fake 200 OK without saving spam
            if (!string.IsNullOrWhiteSpace(req.Website))
            {
                return Results.Ok(new LeadSubmissionResponse(
                    "cnt_honeypot",
                    $"{leadOpts.ContactReferencePrefix}-0000",
                    businessOptions.CurrentValue.WhatsApp.BuildUrl(),
                    null));
            }

            var errors = LeadService.ValidateContactFields(
                req.Name,
                req.Phone,
                req.Email,
                req.Address,
                req.ConsentGiven,
                leadOpts.RequireAddress,
                options: leadOpts);

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var result = await leads.SubmitContactAsync(req, http, ct);
            return Results.Created($"/api/contacts/{result.Id}", result);
        })
        .RequireRateLimiting(LeadRateLimitPolicy)
        .WithTags("Leads");

        app.MapPost("/api/custom-requests", async (
            CreateCustomRequest req,
            HttpContext http,
            LeadService leads,
            IOptionsMonitor<LeadOptions> leadOptions,
            IOptionsMonitor<BusinessOptions> businessOptions,
            CancellationToken ct) =>
        {
            var leadOpts = leadOptions.CurrentValue;

            if (!string.IsNullOrWhiteSpace(req.Website))
            {
                return Results.Ok(new LeadSubmissionResponse(
                    "creq_honeypot",
                    $"{leadOpts.CustomRequestReferencePrefix}-0000",
                    businessOptions.CurrentValue.WhatsApp.BuildUrl(),
                    new QuoteSnapshot()));
            }

            if (req.Contact is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["contact"] = ["Contact details are required."]
                });
            }

            var errors = LeadService.ValidateContactFields(
                req.Contact.Name,
                req.Contact.Phone,
                req.Contact.Email,
                req.Contact.Address,
                req.ConsentGiven,
                leadOpts.RequireAddress,
                prefix: "contact",
                options: leadOpts);

            if (string.IsNullOrWhiteSpace(req.ProductId))
                errors["productId"] = ["Please choose a product."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            try
            {
                var result = await leads.SubmitCustomRequestAsync(req, http, ct);
                return Results.Created($"/api/custom-requests/{result.Id}", result);
            }
            catch (LeadValidationException vex)
            {
                return Results.ValidationProblem(vex.Errors);
            }
            catch (KeyNotFoundException kex)
            {
                return Results.NotFound(new { message = kex.Message });
            }
            catch (ArgumentException aex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["selectedRegion"] = [aex.Message]
                });
            }
        })
        .RequireRateLimiting(LeadRateLimitPolicy)
        .WithTags("Leads");

        app.MapPost("/api/uploads/temp", async (
            IFormFile file,
            CustomerUploadStore store,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Please select an image file to upload."]
                });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var saved = await store.SaveTempAsync(stream, file.Length, file.FileName, ct);
                return Results.Ok(saved);
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = [ex.Message]
                });
            }
        })
        .DisableAntiforgery()
        .RequireRateLimiting(UploadRateLimitPolicy)
        .WithTags("Leads");

        return app;
    }
}

public sealed record CreateContactRequest(
    string Name,
    string Phone,
    string Email,
    string? Address,
    string? Message,
    string? ProductSlug,
    string? SelectedRegion,
    string? SourcePage,
    bool ConsentGiven,
    string? Website = null);

public sealed record CreateCustomRequest(
    string ProductId,
    string? SelectedRegion,
    List<OptionSelection>? Selections,
    List<string>? Extras,
    List<string>? TempImageIds,
    string? Notes,
    CustomerContactInfo Contact,
    bool ConsentGiven,
    string? Website = null);

public sealed record LeadSubmissionResponse(
    string Id,
    string Reference,
    string WhatsappUrl,
    QuoteSnapshot? QuotedPrice);
