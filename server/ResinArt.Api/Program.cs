using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Admin;
using ResinArt.Api.Features.Auth;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Leads;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Features.Seo;
using ResinArt.Api.Infrastructure.Images;
using ResinArt.Api.Infrastructure.Jobs;
using ResinArt.Api.Infrastructure.Notifications;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// 1. Layer {DataRoot}/config/appsettings.Business.json after all host/test configuration sources are registered.
#pragma warning disable ASP0013
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    var interim = config.Build();
    var root = DataRoot.FromConfiguration(interim, hostingContext.HostingEnvironment);
    config.AddJsonFile(root.BusinessSettingsFile, optional: true, reloadOnChange: true);
    config.AddEnvironmentVariables();
});
#pragma warning restore ASP0013

builder.Services.AddSingleton(sp =>
    DataRoot.FromConfiguration(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>()));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));

// 2. Strongly-typed options with startup validation.
builder.Services.AddOptions<BusinessOptions>()
    .BindConfiguration(BusinessOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RegionOptions>()
    .BindConfiguration(RegionOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<GeoIpOptions>()
    .BindConfiguration(GeoIpOptions.Section);
builder.Services.AddOptions<LeadOptions>()
    .BindConfiguration(LeadOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<FeatureOptions>()
    .BindConfiguration(FeatureOptions.Section);
builder.Services.AddOptions<SeoOptions>()
    .BindConfiguration(SeoOptions.Section);
builder.Services.AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>()
    .BindConfiguration(SecurityOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RateLimitingOptions>()
    .BindConfiguration(RateLimitingOptions.Section);
builder.Services.AddOptions<UploadOptions>()
    .BindConfiguration(UploadOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<CatalogOptions>()
    .BindConfiguration(CatalogOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<MaintenanceOptions>()
    .BindConfiguration(MaintenanceOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<NotificationOptions>()
    .BindConfiguration(NotificationOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 3. Data Protection & Cookie Authentication configured via appsettings.json.
var securityBootstrap = builder.Configuration.GetSection(SecurityOptions.Section).Get<SecurityOptions>() ?? new SecurityOptions();
var adminBootstrap = builder.Configuration.GetSection(AdminOptions.Section).Get<AdminOptions>() ?? new AdminOptions();
var rateLimitBootstrap = builder.Configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
var storageBootstrap = builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();

var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName(securityBootstrap.ApplicationName);
if (securityBootstrap.PersistDataProtectionKeys)
{
    var startupRoot = DataRoot.FromConfiguration(builder.Configuration, builder.Environment);
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(startupRoot.KeysPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(AdminAuthEndpoints.CookieScheme)
    .AddCookie(AdminAuthEndpoints.CookieScheme, options =>
    {
        options.Cookie.Name = adminBootstrap.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(Math.Max(1, adminBootstrap.SessionExpireHours));
        options.SlidingExpiration = adminBootstrap.SlidingExpiration;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminAuthEndpoints.AdminPolicy, policy => policy.RequireRole(adminBootstrap.Role));

// 4. JSON serialisation, OpenAPI & per-IP rate limiting on public submissions.
builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Apply(o.SerializerOptions));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(LeadEndpoints.LeadRateLimitPolicy, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, rateLimitBootstrap.Leads.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, rateLimitBootstrap.Leads.WindowSeconds)),
                QueueLimit = Math.Max(0, rateLimitBootstrap.Leads.QueueLimit)
            }));
    options.AddPolicy(LeadEndpoints.UploadRateLimitPolicy, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, rateLimitBootstrap.Uploads.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, rateLimitBootstrap.Uploads.WindowSeconds)),
                QueueLimit = Math.Max(0, rateLimitBootstrap.Uploads.QueueLimit)
            }));
});

// 5. JSON document collections in {dataRoot}/db/*.json.
var collections = storageBootstrap.Collections;
builder.Services.AddJsonCollection<CategoryDocument>(collections.Categories);
builder.Services.AddJsonCollection<ProductDocument>(collections.Products);
builder.Services.AddJsonCollection<PricingRulesDocument>(collections.PricingRules);
builder.Services.AddJsonCollection<ContactDocument>(collections.Contacts);
builder.Services.AddJsonCollection<CustomRequestDocument>(collections.CustomRequests);
builder.Services.AddJsonCollection<CounterDocument>(collections.Counters);
builder.Services.AddJsonCollection<AdminUserDocument>(collections.AdminUsers);

// 6. Application services & background workers.
builder.Services.AddSingleton<RegionResolver>();
builder.Services.AddSingleton<IIpCountryLookup, IpCountryLookup>();
builder.Services.AddSingleton<PricingEngine>();
builder.Services.AddSingleton<SpaMetaService>();
builder.Services.AddSingleton<CustomerUploadStore>();
builder.Services.AddSingleton<LeadNotificationService>();
builder.Services.AddSingleton<ILeadNotificationQueue>(sp => sp.GetRequiredService<LeadNotificationService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<LeadNotificationService>());
builder.Services.AddSingleton<LeadService>();
builder.Services.AddSingleton<AdminAuthService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<DataMaintenanceService>();
builder.Services.AddHostedService<DataMaintenanceHostedService>();

var app = builder.Build();
var dataRoot = app.Services.GetRequiredService<DataRoot>();
var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Security headers for public & admin routes (driven by SecurityOptions).
app.Use(async (ctx, next) =>
{
    var sec = ctx.RequestServices.GetRequiredService<IOptionsMonitor<SecurityOptions>>().CurrentValue;
    if (!string.IsNullOrWhiteSpace(sec.ContentTypeOptions))
        ctx.Response.Headers.XContentTypeOptions = sec.ContentTypeOptions;
    if (!string.IsNullOrWhiteSpace(sec.FrameOptions))
        ctx.Response.Headers.XFrameOptions = sec.FrameOptions;
    if (!string.IsNullOrWhiteSpace(sec.ReferrerPolicy))
        ctx.Response.Headers["Referrer-Policy"] = sec.ReferrerPolicy;
    if (!string.IsNullOrWhiteSpace(sec.PermissionsPolicy))
        ctx.Response.Headers["Permissions-Policy"] = sec.PermissionsPolicy;
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Seed sample catalogue, images and initial admin account on first run.
if (app.Configuration.GetValue($"{StorageOptions.Section}:{nameof(StorageOptions.SeedSampleData)}", app.Environment.IsDevelopment()))
{
    await DataSeeder.SeedIfEmptyAsync(app.Services);
}
await app.Services.GetRequiredService<AdminAuthService>().SeedDefaultAdminIfEmptyAsync();

// Serve uploaded product & request images from the persistent data root at /uploads/*.
var uploadsRequestPath = string.IsNullOrWhiteSpace(storageOptions.UploadsRequestPath) ? "/uploads" : storageOptions.UploadsRequestPath;
var uploadsCacheHeader = $"public,max-age={Math.Max(0, storageOptions.UploadsCacheMaxAgeSeconds)}";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(dataRoot.UploadsPath),
    RequestPath = uploadsRequestPath,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = uploadsCacheHeader;
    }
});

// Serve compiled React SPA assets from wwwroot when present.
app.UseStaticFiles();

// API endpoints.
app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }))
    .ExcludeFromDescription();

app.MapRegionEndpoints();
app.MapCatalogEndpoints();
app.MapPricingEndpoints();
app.MapLeadEndpoints();
app.MapAdminAuthEndpoints();
app.MapAdminEndpoints();
app.MapSeoEndpoints();

// Any unmatched /api/* path returns a proper 404 JSON ProblemDetails instead of the SPA HTML shell.
app.Map("/api/{**catchAll}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "API endpoint not found."))
    .ExcludeFromDescription();

// SPA fallback with server-side Open Graph / JSON-LD meta injection for shared product links.
app.MapFallback((HttpContext http, SpaMetaService seo, CancellationToken ct) => seo.RenderIndexAsync(http, ct));

app.Run();

public partial class Program;
