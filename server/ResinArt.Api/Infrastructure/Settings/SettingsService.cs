using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Infrastructure.Settings;

/// <summary>
/// Manages the admin-editable configuration layer (<c>{dataRoot}/config/appsettings.Business.json</c>).
/// Validates all sections before writing, snapshots the previous file into <c>{dataRoot}/backups/</c>,
/// performs an atomic temp-file swap, and calls <see cref="IConfigurationRoot.Reload"/> so <see cref="IOptionsMonitor{TOptions}"/>
/// updates immediately without restarting the server (solves C3 in the architecture review).
/// </summary>
public sealed class SettingsService(
    DataRoot dataRoot,
    IConfiguration configuration,
    IOptionsMonitor<BusinessOptions> business,
    IOptionsMonitor<RegionOptions> regions,
    IOptionsMonitor<LeadOptions> leads,
    IOptionsMonitor<FeatureOptions> features,
    IOptionsMonitor<SeoOptions> seo,
    IOptionsMonitor<UploadOptions> uploads,
    IOptionsMonitor<CatalogOptions> catalog,
    IOptionsMonitor<MaintenanceOptions> maintenance,
    ILogger<SettingsService> logger)
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public EditableSettingsPayload GetCurrent()
    {
        var maxRecent = Math.Max(1, maintenance.CurrentValue.MaxRecentSettingsBackups);
        var backups = Directory.Exists(dataRoot.BackupsPath)
            ? Directory.GetFiles(dataRoot.BackupsPath, "appsettings.Business.*.json")
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .OrderByDescending(n => n)
                .Take(maxRecent)
                .Cast<string>()
                .ToList()
            : [];

        return new EditableSettingsPayload(
            Business: business.CurrentValue,
            Regions: regions.CurrentValue,
            Leads: leads.CurrentValue,
            Features: features.CurrentValue,
            Seo: seo.CurrentValue,
            ConfigFilePath: dataRoot.BusinessSettingsFile,
            RecentBackups: backups,
            Uploads: uploads.CurrentValue,
            Catalog: catalog.CurrentValue,
            Maintenance: maintenance.CurrentValue);
    }

    public async Task<SettingsUpdateResult> UpdateAsync(UpdateSettingsRequest req, CancellationToken ct = default)
    {
        var errors = ValidatePayload(req);
        if (errors.Count > 0)
            throw new SettingsValidationException(errors);

        await _writeLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(dataRoot.ConfigPath);
            Directory.CreateDirectory(dataRoot.BackupsPath);

            string? backupFileName = null;
            if (File.Exists(dataRoot.BusinessSettingsFile))
            {
                backupFileName = $"appsettings.Business.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json";
                var backupPath = Path.Combine(dataRoot.BackupsPath, backupFileName);
                File.Copy(dataRoot.BusinessSettingsFile, backupPath, overwrite: true);
            }

            // Use PascalCase top-level keys matching standard appsettings.json conventions
            var document = new Dictionary<string, object>
            {
                [BusinessOptions.Section] = req.Business,
                [RegionOptions.Section] = req.Regions,
                [LeadOptions.Section] = req.Leads,
                [FeatureOptions.Section] = req.Features,
                [SeoOptions.Section] = req.Seo,
                [UploadOptions.Section] = req.Uploads ?? uploads.CurrentValue,
                [CatalogOptions.Section] = req.Catalog ?? catalog.CurrentValue,
                [MaintenanceOptions.Section] = req.Maintenance ?? maintenance.CurrentValue
            };

            var tempPath = dataRoot.BusinessSettingsFile + ".tmp";
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                             bufferSize: 32 * 1024, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(fs, document, IndentedJsonOptions, ct);
            }

            File.Move(tempPath, dataRoot.BusinessSettingsFile, overwrite: true);

            if (configuration is IConfigurationRoot root)
            {
                root.Reload();
            }

            logger.LogInformation("Updated {File} (backup: {Backup}) and reloaded configuration.",
                dataRoot.BusinessSettingsFile, backupFileName ?? "none");

            return new SettingsUpdateResult(
                SavedAt: DateTimeOffset.UtcNow,
                BackupFileName: backupFileName,
                Settings: GetCurrent());
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static Dictionary<string, string[]> ValidatePayload(UpdateSettingsRequest req)
    {
        var errors = new Dictionary<string, string[]>();

        void Check(object? instance, string prefix, bool required = true)
        {
            if (instance is null)
            {
                if (required)
                    errors[prefix] = [$"{prefix} section is required."];
                return;
            }
            var results = new List<ValidationResult>();
            var ctx = new ValidationContext(instance);
            Validator.TryValidateObject(instance, ctx, results, validateAllProperties: true);
            foreach (var r in results)
            {
                var member = r.MemberNames.FirstOrDefault() ?? prefix;
                var key = $"{prefix}.{member}";
                errors[key] = [r.ErrorMessage ?? "Invalid value."];
            }
        }

        Check(req.Business, "business");
        Check(req.Business?.WhatsApp, "business.whatsApp");
        Check(req.Regions, "regions");
        Check(req.Leads, "leads");
        Check(req.Features, "features");
        Check(req.Seo, "seo");
        Check(req.Uploads, "uploads", required: false);
        Check(req.Catalog, "catalog", required: false);
        Check(req.Maintenance, "maintenance", required: false);

        return errors;
    }
}

public sealed record EditableSettingsPayload(
    BusinessOptions Business,
    RegionOptions Regions,
    LeadOptions Leads,
    FeatureOptions Features,
    SeoOptions Seo,
    string ConfigFilePath,
    IReadOnlyList<string> RecentBackups,
    UploadOptions? Uploads = null,
    CatalogOptions? Catalog = null,
    MaintenanceOptions? Maintenance = null);

public sealed record UpdateSettingsRequest(
    BusinessOptions Business,
    RegionOptions Regions,
    LeadOptions Leads,
    FeatureOptions Features,
    SeoOptions Seo,
    UploadOptions? Uploads = null,
    CatalogOptions? Catalog = null,
    MaintenanceOptions? Maintenance = null);

public sealed record SettingsUpdateResult(
    DateTimeOffset SavedAt,
    string? BackupFileName,
    EditableSettingsPayload Settings);

public sealed class SettingsValidationException(Dictionary<string, string[]> errors) : Exception("Invalid settings.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
