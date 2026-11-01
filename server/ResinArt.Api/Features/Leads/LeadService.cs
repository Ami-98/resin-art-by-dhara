using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Images;
using ResinArt.Api.Infrastructure.Notifications;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Leads;

public sealed partial class LeadService(
    IDocumentStore<ContactDocument> contacts,
    IDocumentStore<CustomRequestDocument> customRequests,
    IDocumentStore<CounterDocument> counters,
    IDocumentStore<ProductDocument> products,
    IDocumentStore<PricingRulesDocument> pricingRules,
    RegionResolver regions,
    IIpCountryLookup ipLookup,
    PricingEngine pricingEngine,
    CustomerUploadStore uploads,
    ILeadNotificationQueue notifications,
    IOptionsMonitor<BusinessOptions> businessOptions,
    IOptionsMonitor<LeadOptions> leadOptions)
{
    private readonly SemaphoreSlim _counterLock = new(1, 1);

    public async Task<LeadSubmissionResponse> SubmitContactAsync(
        CreateContactRequest req,
        HttpContext http,
        CancellationToken ct = default)
    {
        var leadOpts = leadOptions.CurrentValue;
        var selectedRegion = regions.Normalize(req.SelectedRegion) ?? regions.DefaultRegion;
        var detectedRegion = regions.FromCountry(ipLookup.GetCountry(http));
        var phoneCountryCode = GetRegionPhoneDialCode(selectedRegion, leadOpts.DefaultPhoneCountryCode);
        var normalizedPhone = NormalizePhone(req.Phone, phoneCountryCode);
        var reference = await NextReferenceAsync("contact", leadOpts.ContactReferencePrefix, leadOpts.CounterStartValue, ct);
        var now = DateTimeOffset.UtcNow;

        string? productName = null;
        if (!string.IsNullOrWhiteSpace(req.ProductSlug))
        {
            var found = await products.FindByIdOrSlugAsync(req.ProductSlug, publishedOnly: false, ct);
            productName = found?.Name;
        }

        var doc = new ContactDocument
        {
            Id = $"cnt_{Guid.NewGuid():N}"[..20],
            Reference = reference,
            Name = req.Name.Trim(),
            Phone = normalizedPhone,
            Email = req.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim(),
            Message = string.IsNullOrWhiteSpace(req.Message) ? null : req.Message.Trim(),
            ProductSlug = string.IsNullOrWhiteSpace(req.ProductSlug) ? null : req.ProductSlug.Trim(),
            ProductName = productName,
            DetectedRegion = detectedRegion,
            SelectedRegion = selectedRegion,
            SourcePage = req.SourcePage,
            Consent = new ConsentRecord
            {
                Given = true,
                PolicyVersion = leadOpts.PrivacyPolicyVersion,
                At = now
            },
            Status = LeadStatus.New,
            CreatedAt = now,
            RetainUntil = now.AddMonths(leadOpts.RetentionMonths),
            Meta = BuildMeta(http, detectedRegion, selectedRegion, leadOpts)
        };

        await contacts.UpsertAsync(doc, ct: ct);

        notifications.Enqueue(new LeadNotificationEvent(
            Kind: "Contact",
            Reference: reference,
            CustomerName: doc.Name,
            CustomerPhone: doc.Phone,
            CustomerEmail: doc.Email,
            SelectedRegion: doc.SelectedRegion,
            ProductName: productName,
            SummaryText: doc.Message,
            CreatedAt: now));

        var whatsappMessage = BuildContactWhatsAppMessage(doc);
        var whatsappUrl = businessOptions.CurrentValue.WhatsApp.BuildUrlWithText(whatsappMessage);
        return new LeadSubmissionResponse(doc.Id, reference, whatsappUrl, null);
    }

    public async Task<LeadSubmissionResponse> SubmitCustomRequestAsync(
        CreateCustomRequest req,
        HttpContext http,
        CancellationToken ct = default)
    {
        var selectedRegion = regions.Normalize(req.SelectedRegion)
            ?? throw new ArgumentException($"Unsupported region '{req.SelectedRegion}'.");

        var product = await products.FindByIdOrSlugAsync(req.ProductId, publishedOnly: true, ct)
            ?? throw new KeyNotFoundException($"Product '{req.ProductId}' was not found.");

        // Validate required product options
        var selectionMap = (req.Selections ?? []).ToDictionary(s => s.OptionId, StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>();
        foreach (var opt in product.Options.Where(o => o.Required))
        {
            selectionMap.TryGetValue(opt.Id, out var sel);
            var satisfied = opt.Type switch
            {
                OptionType.Select => !string.IsNullOrWhiteSpace(sel?.ChoiceId) &&
                                     opt.Choices.Any(c => string.Equals(c.Id, sel.ChoiceId, StringComparison.OrdinalIgnoreCase)),
                OptionType.Text => !string.IsNullOrWhiteSpace(sel?.Value),
                OptionType.Image => sel?.Provided == true || !string.IsNullOrWhiteSpace(sel?.Value) || (req.TempImageIds?.Count > 0),
                OptionType.Boolean => true,
                _ => true
            };
            if (!satisfied)
                errors[$"selections.{opt.Id}"] = [$"'{opt.Label}' is required."];
        }
        if (errors.Count > 0)
            throw new LeadValidationException(errors);

        var requestId = $"creq_{Guid.NewGuid():N}"[..20];
        var promotedUrls = uploads.PromoteTempUploads(requestId, req.TempImageIds);

        // If any image option was selected or photos were uploaded, mark image options as provided for accurate pricing
        var pricingSelections = (req.Selections ?? []).Select(s =>
        {
            var opt = product.Options.FirstOrDefault(o => string.Equals(o.Id, s.OptionId, StringComparison.OrdinalIgnoreCase));
            if (opt?.Type == OptionType.Image && promotedUrls.Count > 0)
                return s with { Provided = true };
            return s;
        }).ToList();

        var leadOpts = leadOptions.CurrentValue;
        var rulesTask = pricingRules.GetRulesAsync(ct);
        var referenceTask = NextReferenceAsync("customRequest", leadOpts.CustomRequestReferencePrefix, leadOpts.CounterStartValue, ct);
        await Task.WhenAll(rulesTask, referenceTask);

        var rules = await rulesTask;
        var reference = await referenceTask;
        var quoteResult = pricingEngine.CalculateQuote(product, rules, selectedRegion, pricingSelections, req.Extras);

        var detectedRegion = regions.FromCountry(ipLookup.GetCountry(http));
        var now = DateTimeOffset.UtcNow;

        var detailedSelections = new List<CustomRequestSelection>();
        foreach (var opt in product.Options)
        {
            if (!selectionMap.TryGetValue(opt.Id, out var sel)) continue;
            var choice = opt.Choices.FirstOrDefault(c => string.Equals(c.Id, sel.ChoiceId, StringComparison.OrdinalIgnoreCase));
            detailedSelections.Add(new CustomRequestSelection
            {
                OptionId = opt.Id,
                OptionLabel = opt.Label,
                ChoiceId = choice?.Id ?? sel.ChoiceId,
                ChoiceLabel = choice?.Label,
                Value = sel.Value,
                Provided = sel.Provided ?? (opt.Type == OptionType.Image && promotedUrls.Count > 0 ? true : null)
            });
        }

        var snapshot = new QuoteSnapshot
        {
            Total = quoteResult.Total,
            Currency = quoteResult.Currency,
            Lines = quoteResult.Lines.ToList(),
            ShippingNote = quoteResult.ShippingNote,
            TaxLabel = quoteResult.TaxLabel,
            PricingRulesVersion = rules.Version
        };

        var doc = new CustomRequestDocument
        {
            Id = requestId,
            Reference = reference,
            ProductId = product.Id,
            ProductSlug = product.Slug,
            ProductNameSnapshot = product.Name,
            DetectedRegion = detectedRegion,
            SelectedRegion = selectedRegion,
            Selections = detailedSelections,
            Extras = (req.Extras ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReferenceImages = promotedUrls.ToList(),
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            Contact = new CustomerContactInfo
            {
                Name = req.Contact.Name.Trim(),
                Phone = NormalizePhone(req.Contact.Phone, GetRegionPhoneDialCode(selectedRegion, leadOpts.DefaultPhoneCountryCode)),
                Email = req.Contact.Email.Trim(),
                Address = string.IsNullOrWhiteSpace(req.Contact.Address) ? null : req.Contact.Address.Trim()
            },
            Consent = new ConsentRecord
            {
                Given = true,
                PolicyVersion = leadOpts.PrivacyPolicyVersion,
                At = now
            },
            Quote = snapshot,
            Status = LeadStatus.New,
            CreatedAt = now,
            RetainUntil = now.AddMonths(leadOpts.RetentionMonths),
            Meta = BuildMeta(http, detectedRegion, selectedRegion, leadOpts)
        };

        await customRequests.UpsertAsync(doc, ct: ct);

        notifications.Enqueue(new LeadNotificationEvent(
            Kind: "CustomRequest",
            Reference: reference,
            CustomerName: doc.Contact.Name,
            CustomerPhone: doc.Contact.Phone,
            CustomerEmail: doc.Contact.Email,
            SelectedRegion: doc.SelectedRegion,
            ProductName: doc.ProductNameSnapshot,
            SummaryText: $"{snapshot.Currency} {snapshot.Total / 100m:0.00}",
            CreatedAt: now));

        var whatsappMessage = BuildCustomRequestWhatsAppMessage(doc);
        var whatsappUrl = businessOptions.CurrentValue.WhatsApp.BuildUrlWithText(whatsappMessage);
        return new LeadSubmissionResponse(doc.Id, reference, whatsappUrl, snapshot);
    }

    private static string GetRegionPhoneDialCode(string? regionCode, string fallback) =>
        regionCode?.ToUpperInvariant() switch
        {
            "US" or "CA" => "+1",
            "GJ-IN" or "IN" => "+91",
            _ => fallback
        };

    private static string FormatRegionDisplayName(string? regionCode) =>
        regionCode?.ToUpperInvariant() switch
        {
            "GJ-IN" => "India",
            "US" => "United States",
            "CA" => "Canada",
            _ => regionCode ?? ""
        };

    private static string BuildContactWhatsAppMessage(ContactDocument doc)
    {
        var lines = new List<string>
        {
            "✨ *Hi Dhara! I'm interested in your handcrafted Resin Art.* 🌸",
            "",
            "Here are my details:",
            $"👤 *Name:* {doc.Name}",
            $"📞 *Phone:* {doc.Phone}",
            $"✉️ *Email:* {doc.Email}"
        };

        var regionLabel = FormatRegionDisplayName(doc.SelectedRegion);
        if (!string.IsNullOrWhiteSpace(doc.Address))
        {
            var loc = !string.IsNullOrWhiteSpace(regionLabel) &&
                      !doc.Address.Contains(regionLabel, StringComparison.OrdinalIgnoreCase)
                ? $"{doc.Address} ({regionLabel})"
                : doc.Address;
            lines.Add($"📍 *Location:* {loc}");
        }
        else if (!string.IsNullOrWhiteSpace(regionLabel))
        {
            lines.Add($"📍 *Location:* {regionLabel}");
        }

        var productLabel = doc.ProductName ?? doc.ProductSlug?.Replace('-', ' ');
        if (!string.IsNullOrWhiteSpace(productLabel))
            lines.Add($"🎨 *Interested In:* {productLabel}");

        if (!string.IsNullOrWhiteSpace(doc.Message))
        {
            lines.Add("");
            lines.Add($"💬 *My Notes / Idea:*");
            lines.Add($"\"{doc.Message}\"");
        }

        lines.Add("");
        lines.Add("Looking forward to planning my custom piece with you! 💛");

        return string.Join("\n", lines);
    }

    private static string BuildCustomRequestWhatsAppMessage(CustomRequestDocument doc)
    {
        var lines = new List<string>
        {
            "✨ *Hi Dhara! I'd love to order a custom Resin Art piece.* 🌸",
            "",
            $"🎨 *Selected Piece:* {doc.ProductNameSnapshot}"
        };

        var selectionLines = new List<string>();
        foreach (var sel in doc.Selections)
        {
            var val = sel.ChoiceLabel ?? sel.Value ?? (sel.Provided == true ? "Yes" : null);
            if (!string.IsNullOrWhiteSpace(val))
                selectionLines.Add($"  • *{sel.OptionLabel}:* {val}");
        }

        if (selectionLines.Count > 0)
        {
            lines.Add("🪄 *Customizations:*");
            lines.AddRange(selectionLines);
        }

        if (doc.Extras.Count > 0)
            lines.Add($"🎁 *Add-Ons:* {string.Join(", ", doc.Extras)}");

        lines.Add($"💎 *Estimated Quote:* {doc.Quote.Currency} {doc.Quote.Total / 100m:0.00}");
        lines.Add("");
        lines.Add("👤 *My Contact Details:*");
        lines.Add($"  • *Name:* {doc.Contact.Name}");
        lines.Add($"  • *Phone:* {doc.Contact.Phone}");
        lines.Add($"  • *Email:* {doc.Contact.Email}");

        var regionLabel = FormatRegionDisplayName(doc.SelectedRegion);
        if (!string.IsNullOrWhiteSpace(doc.Contact.Address))
        {
            var loc = !string.IsNullOrWhiteSpace(regionLabel) &&
                      !doc.Contact.Address.Contains(regionLabel, StringComparison.OrdinalIgnoreCase)
                ? $"{doc.Contact.Address} ({regionLabel})"
                : doc.Contact.Address;
            lines.Add($"  • *Location:* {loc}");
        }
        else if (!string.IsNullOrWhiteSpace(regionLabel))
        {
            lines.Add($"  • *Location:* {regionLabel}");
        }

        if (!string.IsNullOrWhiteSpace(doc.Notes))
        {
            lines.Add("");
            lines.Add("💬 *Special Notes:*");
            lines.Add($"\"{doc.Notes}\"");
        }

        lines.Add("");
        lines.Add("Looking forward to creating this special keepsake with you! 💛");

        return string.Join("\n", lines);
    }

    public static Dictionary<string, string[]> ValidateContactFields(
        string? name,
        string? phone,
        string? email,
        string? address,
        bool consentGiven,
        bool requireAddress,
        string prefix = "",
        LeadOptions? options = null)
    {
        var opts = options ?? new LeadOptions();
        var errors = new Dictionary<string, string[]>();
        string Key(string field) => string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < opts.MinNameLength)
            errors[Key("name")] = [$"Please enter your full name (at least {opts.MinNameLength} characters)."];

        var cleanedPhone = NormalizePhone(phone ?? "", opts.DefaultPhoneCountryCode);
        if (!PhoneRegex().IsMatch(cleanedPhone))
            errors[Key("phone")] = ["Please enter a valid 8–15 digit phone number (with country code if outside India)."];

        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email.Trim()))
            errors[Key("email")] = ["Please enter a valid email address."];

        if (requireAddress && (string.IsNullOrWhiteSpace(address) || address.Trim().Length < opts.MinAddressLength))
            errors[Key("address")] = ["Delivery address is required."];

        if (!consentGiven)
            errors["consentGiven"] = ["Please agree to be contacted regarding your enquiry."];

        return errors;
    }

    public static string NormalizePhone(string raw, string defaultCountryCode = "+91")
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var trimmed = raw.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "";
        if (digits.Length == 10 && !trimmed.StartsWith('+'))
        {
            var prefix = string.IsNullOrWhiteSpace(defaultCountryCode) ? "+91" : defaultCountryCode.Trim();
            if (!prefix.StartsWith('+')) prefix = "+" + prefix;
            return prefix + digits;
        }
        return "+" + digits;
    }

    private async Task<string> NextReferenceAsync(string counterId, string prefix, long startValue, CancellationToken ct)
    {
        await _counterLock.WaitAsync(ct);
        try
        {
            var existing = await counters.GetAsync(counterId, ct);
            var nextValue = (existing?.Value ?? startValue) + 1;
            await counters.UpsertAsync(new CounterDocument { Id = counterId, Value = nextValue }, ct: ct);
            return $"{prefix}-{DateTimeOffset.UtcNow:yyyy}-{nextValue:0000}";
        }
        finally
        {
            _counterLock.Release();
        }
    }

    private static LeadMeta BuildMeta(HttpContext http, string? detectedRegion, string selectedRegion, LeadOptions opts)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString();
        string? ipHash = null;
        if (!string.IsNullOrWhiteSpace(ip))
        {
            var salt = string.IsNullOrWhiteSpace(opts.IpHashSalt) ? "resinart-salt:" : opts.IpHashSalt;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ip));
            ipHash = Convert.ToHexStringLower(bytes.AsSpan(0, 8));
        }

        var maxUa = Math.Max(32, opts.MaxUserAgentLength);
        return new LeadMeta
        {
            IpHash = ipHash,
            UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
                ? ua[..Math.Min(ua.Length, maxUa)]
                : null,
            RegionMismatch = detectedRegion is not null &&
                             !string.Equals(detectedRegion, selectedRegion, StringComparison.OrdinalIgnoreCase)
        };
    }

    [GeneratedRegex(@"^\+\d{8,15}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}

public sealed class LeadValidationException(Dictionary<string, string[]> errors) : Exception("Validation failed.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
