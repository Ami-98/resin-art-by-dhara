namespace ResinArt.Api.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Persistent data root. Relative paths are resolved against the content root. Must live outside the deploy folder in production.</summary>
    public string RootPath { get; set; } = "../../data";

    /// <summary>Seed the sample catalogue when the data root is empty (Development default).</summary>
    public bool SeedSampleData { get; set; }

    /// <summary>Public URL path prefix for serving uploaded images.</summary>
    public string UploadsRequestPath { get; set; } = "/uploads";

    /// <summary>HTTP Cache-Control max-age (in seconds) for static uploaded images.</summary>
    public int UploadsCacheMaxAgeSeconds { get; set; } = 604800;

    /// <summary>File name for the admin-editable configuration layer inside {DataRoot}/config/.</summary>
    public string BusinessSettingsFileName { get; set; } = "appsettings.Business.json";

    /// <summary>JSON collection file names inside {DataRoot}/db/.</summary>
    public StorageCollectionNames Collections { get; set; } = new();
}

public sealed class StorageCollectionNames
{
    public string Categories { get; set; } = "categories.json";
    public string Products { get; set; } = "products.json";
    public string PricingRules { get; set; } = "pricing-rules.json";
    public string Contacts { get; set; } = "contacts.json";
    public string CustomRequests { get; set; } = "custom-requests.json";
    public string Counters { get; set; } = "counters.json";
    public string AdminUsers { get; set; } = "admin-users.json";
}

/// <summary>
/// Resolved locations inside the persistent data root. Everything that must survive a redeploy lives here:
/// <code>
/// {root}/db/        JSON collections
/// {root}/config/    admin-edited appsettings.Business.json
/// {root}/uploads/   product + request images (served at /uploads)
/// {root}/backups/   local snapshots
/// {root}/geo/       optional GeoLite2 database
/// {root}/keys/      ASP.NET Data Protection keys
/// </code>
/// </summary>
public sealed class DataRoot
{
    private readonly string _businessSettingsFileName;

    public DataRoot(string rootPath, string businessSettingsFileName = "appsettings.Business.json")
    {
        RootPath = Path.GetFullPath(rootPath);
        _businessSettingsFileName = string.IsNullOrWhiteSpace(businessSettingsFileName)
            ? "appsettings.Business.json"
            : businessSettingsFileName;
    }

    public string RootPath { get; }
    public string DbPath => Path.Combine(RootPath, "db");
    public string ConfigPath => Path.Combine(RootPath, "config");
    public string UploadsPath => Path.Combine(RootPath, "uploads");
    public string BackupsPath => Path.Combine(RootPath, "backups");
    public string GeoPath => Path.Combine(RootPath, "geo");
    public string KeysPath => Path.Combine(RootPath, "keys");
    public string BusinessSettingsFile => Path.Combine(ConfigPath, _businessSettingsFileName);

    public string Resolve(string relativeOrAbsolute)
        => Path.IsPathRooted(relativeOrAbsolute) ? relativeOrAbsolute : Path.Combine(RootPath, relativeOrAbsolute);

    public void EnsureCreated()
    {
        foreach (var dir in new[] { DbPath, ConfigPath, UploadsPath, BackupsPath, GeoPath, KeysPath })
            Directory.CreateDirectory(dir);
    }

    public static DataRoot FromConfiguration(IConfiguration configuration, IHostEnvironment env)
    {
        var configured = configuration[$"{StorageOptions.Section}:{nameof(StorageOptions.RootPath)}"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Storage:RootPath must be configured.");

        var settingsFile = configuration[$"{StorageOptions.Section}:{nameof(StorageOptions.BusinessSettingsFileName)}"]
            ?? "appsettings.Business.json";

        var path = Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
        var root = new DataRoot(path, settingsFile);
        root.EnsureCreated();
        return root;
    }
}
