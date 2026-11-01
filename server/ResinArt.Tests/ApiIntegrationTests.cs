using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResinArt.Api.Features.Admin;
using ResinArt.Api.Features.Auth;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Leads;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Images;
using ResinArt.Api.Infrastructure.Jobs;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Tests;

public sealed class ApiIntegrationTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "resinart-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataRoot);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:RootPath", _dataRoot);
            builder.UseSetting("Storage:SeedSampleData", "true");
        });
    }

    [Fact]
    public async Task Bootstrap_ResolvesCloudflareCountryHeader_ToSuggestedRegion()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/bootstrap");
        req.Headers.Add("CF-IPCountry", "CA");

        using var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<BootstrapResponse>(JsonDefaults.Storage);
        Assert.NotNull(body);
        Assert.Equal("CA", body.SuggestedRegion);
        Assert.Equal("ip", body.SuggestionSource);
        Assert.Equal(3, body.Regions.Count);
    }

    [Fact]
    public async Task ProductsAndQuotes_SwitchBetweenRegions_InrUsdCad()
    {
        using var client = _factory.CreateClient();

        var inrList = await client.GetFromJsonAsync<ProductListResponse>("/api/products?region=GJ-IN", JsonDefaults.Storage);
        var usdList = await client.GetFromJsonAsync<ProductListResponse>("/api/products?region=US", JsonDefaults.Storage);
        var cadList = await client.GetFromJsonAsync<ProductListResponse>("/api/products?region=CA&type=custom", JsonDefaults.Storage);

        Assert.NotNull(inrList);
        Assert.NotNull(usdList);
        Assert.NotNull(cadList);
        Assert.Equal("INR", inrList.Currency);
        Assert.Equal("USD", usdList.Currency);
        Assert.Equal("CAD", cadList.Currency);
        Assert.NotEmpty(inrList.Items);

        // Request a quote in CAD for the ocean wall clock with 15-inch size + roman numerals
        using var quoteRes = await client.PostAsJsonAsync("/api/pricing/quote", new QuoteRequest(
            ProductId: "ocean-wave-wall-clock",
            Region: "CA",
            Selections:
            [
                new OptionSelection("opt_size", ChoiceId: "15in"),
                new OptionSelection("opt_markers", ChoiceId: "roman")
            ],
            Extras: ["giftwrap"]), JsonDefaults.Storage);

        quoteRes.EnsureSuccessStatusCode();
        var quote = await quoteRes.Content.ReadFromJsonAsync<QuoteResult>(JsonDefaults.Storage);
        Assert.NotNull(quote);
        Assert.Equal("CAD", quote.Currency);
        // Base 8499 + 15in 2400 + roman 800 + giftwrap 800 = 12499 (C$124.99)
        Assert.Equal(12499, quote.Total);
    }

    [Fact]
    public async Task ProductRoute_InjectsServerSideOpenGraphAndJsonLd_ForWhatsAppPreviews()
    {
        using var client = _factory.CreateClient();

        using var res = await client.GetAsync("/products/ocean-wave-wall-clock");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("og:title", html);
        Assert.Contains("Wedding Varmala &amp; Couple Portrait Resin Wall Clock", html);
        Assert.Contains("og:image", html);
        Assert.Contains("/uploads/seed/varmala-preservation-couple-clock.jpg", html);
        Assert.Contains("application/ld+json", html);

        // Unknown product slug returns 404 status code
        using var missing = await client.GetAsync("/products/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ContactSubmission_PersistsLead_NormalizesPhone_AndReturnsWhatsAppHiUrl()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/contacts");
        req.Headers.Add("CF-IPCountry", "US");
        req.Content = JsonContent.Create(new CreateContactRequest(
            Name: "Priya Shah",
            Phone: "9876543210",
            Email: "priya@example.com",
            Address: "Ahmedabad, Gujarat",
            Message: "Interested in the ocean clock",
            ProductSlug: "ocean-wave-wall-clock",
            SelectedRegion: "GJ-IN",
            SourcePage: "/contact?product=ocean-wave-wall-clock",
            ConsentGiven: true), options: JsonDefaults.Storage);

        using var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<LeadSubmissionResponse>(JsonDefaults.Storage);
        Assert.NotNull(body);
        Assert.StartsWith("RA-C-", body.Reference);
        Assert.StartsWith("https://wa.me/14378180411?text=", body.WhatsappUrl);
        var decodedContactText = Uri.UnescapeDataString(body.WhatsappUrl.Split("?text=")[1]);
        Assert.Contains("Hi Dhara! I'm interested in your handcrafted Resin Art.", decodedContactText);
        Assert.Contains("*Name:* Priya Shah", decodedContactText);
        Assert.Contains("*Phone:* +919876543210", decodedContactText);
        Assert.Contains("*Email:* priya@example.com", decodedContactText);
        Assert.Contains("*Location:* Ahmedabad, Gujarat (India)", decodedContactText);
        Assert.Contains("\"Interested in the ocean clock\"", decodedContactText);
        Assert.DoesNotContain("Ref:", decodedContactText);

        // Verify persisted in contacts.json with region-mismatch flag (detected US vs selected GJ-IN)
        var store = _factory.Services.GetRequiredService<IDocumentStore<ContactDocument>>();
        var saved = await store.GetAsync(body.Id);
        Assert.NotNull(saved);
        Assert.Equal("+919876543210", saved.Phone);
        Assert.Equal("US", saved.DetectedRegion);
        Assert.Equal("GJ-IN", saved.SelectedRegion);
        Assert.True(saved.Meta.RegionMismatch);
    }

    [Fact]
    public async Task CustomRequest_WithTempPhotoUpload_StripsJpegExif_PromotesPhoto_AndSnapshotsServerQuote()
    {
        using var client = _factory.CreateClient();

        // Build a minimal valid JPEG containing an APP1 (0xFFE1) EXIF segment with fake GPS bytes
        byte[] jpegWithExif =
        [
            0xFF, 0xD8,                                     // SOI
            0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,             // APP0 (preserved)
            0xFF, 0xE1, 0x00, 0x08, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x12, 0x34, // APP1 EXIF (stripped)
            0xFF, 0xDA, 0x00, 0x02, 0x01, 0x02,             // SOS + scan data
            0xFF, 0xD9                                      // EOI
        ];

        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(jpegWithExif);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipart.Add(fileContent, "file", "customer-photo.jpg");

        using var uploadRes = await client.PostAsync("/api/uploads/temp", multipart);
        uploadRes.EnsureSuccessStatusCode();
        var tempUpload = await uploadRes.Content.ReadFromJsonAsync<TempUploadResult>(JsonDefaults.Storage);
        Assert.NotNull(tempUpload);
        Assert.True(tempUpload.SizeBytes < jpegWithExif.Length); // APP1 EXIF segment was stripped!

        // Submit custom request for personalised-resin-purse in US region with custom name + uploaded photo
        var customReq = new CreateCustomRequest(
            ProductId: "personalised-resin-purse",
            SelectedRegion: "US",
            Selections:
            [
                new OptionSelection("opt_palette", ChoiceId: "blush_gold"),
                new OptionSelection("opt_name", Value: "Dhara"),
                new OptionSelection("opt_photo", Provided: true, Value: tempUpload.TempId)
            ],
            Extras: ["giftwrap"],
            TempImageIds: [tempUpload.TempId],
            Notes: "Please use soft pink petals",
            Contact: new CustomerContactInfo
            {
                Name = "Kavya Patel",
                Phone = "+14155550199",
                Email = "kavya@example.com",
                Address = "Edison, NJ"
            },
            ConsentGiven: true);

        using var submitRes = await client.PostAsJsonAsync("/api/custom-requests", customReq, JsonDefaults.Storage);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        var submitBody = await submitRes.Content.ReadFromJsonAsync<LeadSubmissionResponse>(JsonDefaults.Storage);
        Assert.NotNull(submitBody);
        Assert.StartsWith("RA-R-", submitBody.Reference);
        Assert.StartsWith("https://wa.me/14378180411?text=", submitBody.WhatsappUrl);
        var decodedCustomText = Uri.UnescapeDataString(submitBody.WhatsappUrl.Split("?text=")[1]);
        Assert.Contains("Hi Dhara! I'd love to order a custom Resin Art piece.", decodedCustomText);
        Assert.Contains("*Name:* Kavya Patel", decodedCustomText);
        Assert.Contains("\"Please use soft pink petals\"", decodedCustomText);
        Assert.DoesNotContain("Ref:", decodedCustomText);
        Assert.NotNull(submitBody.QuotedPrice);
        // Base $54.99 (5499) + Name $5 (500) + Photo $9 (900) + Giftwrap $6 (600) = 7499 ($74.99)
        Assert.Equal(7499, submitBody.QuotedPrice.Total);
        Assert.Equal("USD", submitBody.QuotedPrice.Currency);

        var reqStore = _factory.Services.GetRequiredService<IDocumentStore<CustomRequestDocument>>();
        var savedReq = await reqStore.GetAsync(submitBody.Id);
        Assert.NotNull(savedReq);
        Assert.Single(savedReq.ReferenceImages);
        Assert.StartsWith($"/uploads/requests/{submitBody.Id}/ref_1.jpg", savedReq.ReferenceImages[0]);
    }

    [Fact]
    public async Task AdminPanel_RequiresCookieAuth_ManagesProductsAndPricing_AndHotReloadsSettings()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        // 1. Unauthenticated request to /api/admin/dashboard is rejected with 401
        using var unauthRes = await client.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

        // 2. Login as seeded studio admin
        using var loginRes = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("dhara", "ResinArt@2026"),
            JsonDefaults.Storage);
        loginRes.EnsureSuccessStatusCode();

        // 3. Create a new published product with only GJ-IN price -> US and CA are derived automatically
        var createReq = new UpsertProductRequest(
            Name: "Emerald Geode Coaster Set",
            Slug: "emerald-geode-coaster-set",
            Category: "wall-clocks",
            Type: ProductType.Standard,
            Status: ProductStatus.Published,
            ShortDescription: "Set of 4 emerald resin coasters with gold rim.",
            Description: "Handcrafted coasters.",
            Images: [],
            BasePrices: new Dictionary<string, RegionalBasePrice>
            {
                ["GJ-IN"] = new() { Amount = 149900, Currency = "INR", IsOverridden = true }
            },
            Options: [],
            LeadTimeDays: 5,
            Tags: ["coasters"],
            IsFeatured: true);

        using var createRes = await client.PostAsJsonAsync("/api/admin/products", createReq, JsonDefaults.Storage);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdProduct = await createRes.Content.ReadFromJsonAsync<ProductDocument>(JsonDefaults.Storage);
        Assert.NotNull(createdProduct);
        Assert.True(createdProduct.BasePrices.ContainsKey("US"));
        Assert.True(createdProduct.BasePrices["US"].Amount > 0);
        Assert.True(createdProduct.BasePrices.ContainsKey("CA"));
        Assert.True(createdProduct.BasePrices["CA"].Amount > 0);

        // 4. Edit appsettings.Business.json via PUT /api/admin/settings and verify immediate hot-reload on /api/bootstrap
        var currentSettings = await client.GetFromJsonAsync<EditableSettingsPayload>("/api/admin/settings", JsonDefaults.Storage);
        Assert.NotNull(currentSettings);

        currentSettings.Business.WhatsApp.Number = "919998887776";
        currentSettings.Business.WhatsApp.DefaultMessage = "Hi Dhara";

        using var saveSettingsRes = await client.PutAsJsonAsync(
            "/api/admin/settings",
            new UpdateSettingsRequest(
                currentSettings.Business,
                currentSettings.Regions,
                currentSettings.Leads,
                currentSettings.Features,
                currentSettings.Seo),
            JsonDefaults.Storage);
        saveSettingsRes.EnsureSuccessStatusCode();

        var bootstrapAfter = await client.GetFromJsonAsync<BootstrapResponse>("/api/bootstrap", JsonDefaults.Storage);
        Assert.NotNull(bootstrapAfter);
        Assert.Equal("https://wa.me/919998887776?text=Hi%20Dhara", bootstrapAfter.Business.WhatsAppUrl);

        // 5. Run snapshot backup & verify downloadable ZIP archive + security headers
        using var backupRes = await client.PostAsync("/api/admin/backups/run", content: null);
        backupRes.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", backupRes.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());

        var report = await backupRes.Content.ReadFromJsonAsync<MaintenanceReport>(JsonDefaults.Storage);
        Assert.NotNull(report?.BackupCreated);
        Assert.EndsWith(".zip", report.BackupCreated.FileName);

        using var zipRes = await client.GetAsync($"/api/admin/backups/{report.BackupCreated.FileName}");
        zipRes.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", zipRes.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { Directory.Delete(_dataRoot, recursive: true); } catch { /* ignore */ }
    }
}
