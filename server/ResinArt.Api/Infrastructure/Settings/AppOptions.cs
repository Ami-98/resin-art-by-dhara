using System.ComponentModel.DataAnnotations;

namespace ResinArt.Api.Infrastructure.Settings;

/// <summary>Admin-editable business settings ("Business" section).</summary>
public sealed class BusinessOptions
{
    public const string Section = "Business";

    [Required, StringLength(80)]
    public string Name { get; set; } = "Resin Art by Dhara";

    [StringLength(120)]
    public string Tagline { get; set; } = "Gujarat · Handmade Resin Studio";

    [StringLength(80)]
    public string Location { get; set; } = "Gujarat, India";

    [StringLength(60)]
    public string OwnerName { get; set; } = "Dhara";

    [EmailAddress]
    public string? ContactEmail { get; set; }

    [Required]
    public WhatsAppOptions WhatsApp { get; set; } = new();

    public SocialOptions Social { get; set; } = new();
}

public sealed class WhatsAppOptions
{
    /// <summary>International format, digits only (e.g. 919812345678).</summary>
    [Required, RegularExpression(@"^\+?\d{8,15}$", ErrorMessage = "WhatsApp number must be 8-15 digits in international format.")]
    public string Number { get; set; } = "910000000000";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://wa.me/";

    [Required, StringLength(200)]
    public string DefaultMessage { get; set; } = "✨ Hi Dhara! I'm interested in your handcrafted Resin Art and would love to know more. 🌸";

    public bool IncludeReference { get; set; }

    /// <summary>Template used in the Admin Inbox when replying to a customer on WhatsApp.</summary>
    [StringLength(300)]
    public string CustomerReplyTemplate { get; set; } =
        "Hi {name}, this is {owner} from {business} regarding your enquiry ({reference}).";

    private string CleanNumber()
    {
        var digits = new string((Number ?? "").Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(digits) ? (Number ?? "").TrimStart('+') : digits;
    }

    /// <summary>Builds the wa.me link with the URL-encoded message.</summary>
    public string BuildUrl(string? reference = null)
    {
        var message = IncludeReference && !string.IsNullOrWhiteSpace(reference)
            ? $"{DefaultMessage} (Ref {reference})"
            : DefaultMessage;
        return BuildUrlWithText(message);
    }

    /// <summary>Builds the wa.me link with a custom pre-filled message.</summary>
    public string BuildUrlWithText(string message)
    {
        return $"{BaseUrl.TrimEnd('/')}/{CleanNumber()}?text={Uri.EscapeDataString(message)}";
    }
}

public sealed class SocialOptions
{
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
}

/// <summary>"Leads" section – contact form and custom request behaviour.</summary>
public sealed class LeadOptions
{
    public const string Section = "Leads";

    public bool RequireAddress { get; set; }

    [Range(1, 120)]
    public int RetentionMonths { get; set; } = 24;

    [Required]
    public string PrivacyPolicyVersion { get; set; } = "2026-10";

    [Required]
    public string ContactReferencePrefix { get; set; } = "RA-C";

    [Required]
    public string CustomRequestReferencePrefix { get; set; } = "RA-R";

    [Range(0, 1_000_000)]
    public long CounterStartValue { get; set; } = 100;

    [Required]
    public string DefaultPhoneCountryCode { get; set; } = "+91";

    [Required]
    public string IpHashSalt { get; set; } = "resinart-salt:";

    [Range(32, 1024)]
    public int MaxUserAgentLength { get; set; } = 200;

    [Range(1, 50)]
    public int MinNameLength { get; set; } = 2;

    [Range(1, 100)]
    public int MinAddressLength { get; set; } = 5;
}

/// <summary>"Features" section – UI feature flags.</summary>
public sealed class FeatureOptions
{
    public const string Section = "Features";

    public bool EnableCustomRequests { get; set; } = true;
    public bool ShowRegionBanner { get; set; } = false;
}

/// <summary>"Seo" section – used for Open Graph / sitemap generation.</summary>
public sealed class SeoOptions
{
    public const string Section = "Seo";

    /// <summary>Canonical public origin, e.g. https://resinartbydhara.com. When empty the request origin is used.</summary>
    public string? PublicBaseUrl { get; set; }

    public string TitleSuffix { get; set; } = "Handcrafted Resin Art from Gujarat";

    public string DefaultDescription { get; set; } =
        "Handmade resin art from Gujarat – wall clocks, personalised purses, mobile covers, name plates and custom gifts.";

    /// <summary>Site-relative or absolute image used when a page has no specific image.</summary>
    public string? DefaultImage { get; set; }

    public string PlaceholderImage { get; set; } = "/uploads/placeholder.jpg";

    public List<string> SitemapStaticPaths { get; set; } = ["/", "/products", "/custom", "/contact"];

    public List<string> RobotsDisallowPaths { get; set; } = ["/admin", "/api"];
}

/// <summary>"Admin" section – studio authentication, session cookie and account lockout policies.</summary>
public sealed class AdminOptions
{
    public const string Section = "Admin";

    [Required]
    public string InitialUserId { get; set; } = "adm_dhara";

    [Required]
    public string InitialUsername { get; set; } = "dhara";

    [Required]
    public string InitialPassword { get; set; } = "ResinArt@2026";

    [Required]
    public string InitialDisplayName { get; set; } = "Dhara (Studio Owner)";

    [Required]
    public string Role { get; set; } = "Admin";

    [Required]
    public string CookieName { get; set; } = "ra_admin";

    [Range(1, 720)]
    public int SessionExpireHours { get; set; } = 12;

    public bool SlidingExpiration { get; set; } = true;

    [Range(1, 50)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;

    [Range(6, 128)]
    public int MinPasswordLength { get; set; } = 8;
}

/// <summary>"Security" section – HTTP response security headers and Data Protection settings.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Security";

    [Required]
    public string ApplicationName { get; set; } = "ResinArtByDhara";

    public bool PersistDataProtectionKeys { get; set; } = true;

    public string ContentTypeOptions { get; set; } = "nosniff";

    public string FrameOptions { get; set; } = "DENY";

    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    public string PermissionsPolicy { get; set; } = "geolocation=(self), camera=(self)";
}

/// <summary>"RateLimiting" section – per-IP rate limit windows for public form and image upload endpoints.</summary>
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public FixedWindowPolicyOptions Leads { get; set; } = new() { PermitLimit = 15, WindowSeconds = 60, QueueLimit = 0 };

    public FixedWindowPolicyOptions Uploads { get; set; } = new() { PermitLimit = 25, WindowSeconds = 300, QueueLimit = 0 };
}

public sealed class FixedWindowPolicyOptions
{
    [Range(1, 10000)]
    public int PermitLimit { get; set; } = 15;

    [Range(1, 86400)]
    public int WindowSeconds { get; set; } = 60;

    [Range(0, 1000)]
    public int QueueLimit { get; set; }
}

/// <summary>"Uploads" section – customer and product image validation and sanitization limits.</summary>
public sealed class UploadOptions
{
    public const string Section = "Uploads";

    [Range(1, 50)]
    public int MaxFileSizeMb { get; set; } = 8;

    [Range(1, 20)]
    public int MaxFilesPerRequest { get; set; } = 5;

    public string TempFilePrefix { get; set; } = "tmp_";

    public string ReferenceFilePrefix { get; set; } = "ref_";

    public bool StripExifMetadata { get; set; } = true;

    public long MaxUploadBytes => MaxFileSizeMb * 1024L * 1024L;
}

/// <summary>"Catalog" section – default product values and fallback asset URLs.</summary>
public sealed class CatalogOptions
{
    public const string Section = "Catalog";

    [Required]
    public string DefaultCategory { get; set; } = "wall-clocks";

    [Range(1, 90)]
    public int DefaultLeadTimeDays { get; set; } = 7;

    [Range(0, 100_000_000)]
    public long DefaultBasePriceMinor { get; set; } = 199900;

    [Required]
    public string PlaceholderImageUrl { get; set; } = "/uploads/placeholder.jpg";

    [Required]
    public string FallbackSlug { get; set; } = "resin-piece";
}

/// <summary>"Maintenance" section – background snapshot backups, pruning and temp upload cleanup schedule.</summary>
public sealed class MaintenanceOptions
{
    public const string Section = "Maintenance";

    [Range(1, 720)]
    public int IntervalHours { get; set; } = 24;

    [Range(1, 365)]
    public int KeepBackupsCount { get; set; } = 30;

    [Range(1, 720)]
    public int OrphanedTempFileMaxAgeHours { get; set; } = 24;

    [Range(1, 100)]
    public int MaxRecentSettingsBackups { get; set; } = 10;
}

/// <summary>"Notifications" section – in-memory lead notification queue bounds.</summary>
public sealed class NotificationOptions
{
    public const string Section = "Notifications";

    [Range(16, 10000)]
    public int QueueCapacity { get; set; } = 256;

    [Range(10, 1000)]
    public int MaxRecentHistory { get; set; } = 100;
}
