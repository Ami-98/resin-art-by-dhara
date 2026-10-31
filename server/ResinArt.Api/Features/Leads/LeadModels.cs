using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Leads;

public enum LeadStatus
{
    New,
    Contacted,
    Quoted,
    Confirmed,
    InProgress,
    Delivered,
    Closed,
    Cancelled
}

public sealed class ConsentRecord
{
    public bool Given { get; set; }
    public string PolicyVersion { get; set; } = "2026-10";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LeadMeta
{
    /// <summary>SHA-256 hash of the visitor IP address (raw IP is never stored for privacy compliance).</summary>
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public bool RegionMismatch { get; set; }
}

/// <summary>Stored in <c>{dataRoot}/db/contacts.json</c>.</summary>
public sealed class ContactDocument : IDocument
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public string? Message { get; set; }
    public string? ProductSlug { get; set; }
    public string? ProductName { get; set; }
    public string? DetectedRegion { get; set; }
    public string SelectedRegion { get; set; } = "GJ-IN";
    public string? SourcePage { get; set; }
    public ConsentRecord Consent { get; set; } = new();
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public string AdminNotes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RetainUntil { get; set; }
    public LeadMeta Meta { get; set; } = new();
}

/// <summary>Stored in <c>{dataRoot}/db/custom-requests.json</c>.</summary>
public sealed class CustomRequestDocument : IDocument
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Reference { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string ProductSlug { get; set; } = "";
    public string ProductNameSnapshot { get; set; } = "";
    public string? DetectedRegion { get; set; }
    public string SelectedRegion { get; set; } = "GJ-IN";
    public List<CustomRequestSelection> Selections { get; set; } = [];
    public List<string> Extras { get; set; } = [];
    public List<string> ReferenceImages { get; set; } = [];
    public string? Notes { get; set; }
    public CustomerContactInfo Contact { get; set; } = new();
    public ConsentRecord Consent { get; set; } = new();
    public QuoteSnapshot Quote { get; set; } = new();
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public string AdminNotes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RetainUntil { get; set; }
    public LeadMeta Meta { get; set; } = new();
}

public sealed class CustomRequestSelection
{
    public string OptionId { get; set; } = "";
    public string OptionLabel { get; set; } = "";
    public string? ChoiceId { get; set; }
    public string? ChoiceLabel { get; set; }
    public string? Value { get; set; }
    public bool? Provided { get; set; }
}

public sealed class CustomerContactInfo
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
}

/// <summary>Immutable snapshot of the server-calculated quote at the time the request was submitted.</summary>
public sealed class QuoteSnapshot
{
    public long Total { get; set; }
    public string Currency { get; set; } = "INR";
    public List<QuoteLine> Lines { get; set; } = [];
    public string ShippingNote { get; set; } = "";
    public string TaxLabel { get; set; } = "";
    public int PricingRulesVersion { get; set; } = 1;
}

/// <summary>Stored in <c>{dataRoot}/db/counters.json</c> for human-friendly reference numbers (e.g. RA-C-0001).</summary>
public sealed class CounterDocument : IDocument
{
    public string Id { get; set; } = "";
    public int Version { get; set; } = 1;
    public long Value { get; set; }
}
