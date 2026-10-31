using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Auth;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Leads;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Images;
using ResinArt.Api.Infrastructure.Jobs;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Admin;

public static partial class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(AdminAuthEndpoints.AdminPolicy)
            .WithTags("Admin");

        // 1. Dashboard summary (uses zero-clone CountAsync queries)
        group.MapGet("/dashboard", async (
            IDocumentStore<ProductDocument> products,
            IDocumentStore<ContactDocument> contacts,
            IDocumentStore<CustomRequestDocument> customRequests,
            CancellationToken ct) =>
        {
            var publishedTask = products.CountAsync(p => p.Status == ProductStatus.Published, ct);
            var draftTask = products.CountAsync(p => p.Status == ProductStatus.Draft, ct);
            var archivedTask = products.CountAsync(p => p.Status == ProductStatus.Archived, ct);
            var newContactsTask = contacts.CountAsync(c => c.Status == LeadStatus.New, ct);
            var totalContactsTask = contacts.CountAsync(ct: ct);
            var newRequestsTask = customRequests.CountAsync(r => r.Status == LeadStatus.New, ct);
            var totalRequestsTask = customRequests.CountAsync(ct: ct);
            var contactMismatchesTask = contacts.CountAsync(c => c.Meta.RegionMismatch, ct);
            var requestMismatchesTask = customRequests.CountAsync(r => r.Meta.RegionMismatch, ct);

            await Task.WhenAll(
                publishedTask, draftTask, archivedTask,
                newContactsTask, totalContactsTask,
                newRequestsTask, totalRequestsTask,
                contactMismatchesTask, requestMismatchesTask);

            return Results.Ok(new AdminDashboardDto(
                PublishedProducts: await publishedTask,
                DraftProducts: await draftTask,
                ArchivedProducts: await archivedTask,
                NewContacts: await newContactsTask,
                TotalContacts: await totalContactsTask,
                NewCustomRequests: await newRequestsTask,
                TotalCustomRequests: await totalRequestsTask,
                RegionMismatches: (await contactMismatchesTask) + (await requestMismatchesTask)));
        });

        // 2. Product management
        group.MapGet("/products", async (
            string? status,
            string? category,
            string? q,
            IDocumentStore<ProductDocument> store,
            CancellationToken ct) =>
        {
            ProductStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<ProductStatus>(status.Trim(), ignoreCase: true, out var ps))
            {
                parsedStatus = ps;
            }
            var catFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            var filtered = await store.QueryAsync(p =>
                (!parsedStatus.HasValue || p.Status == parsedStatus.Value) &&
                (catFilter is null || string.Equals(p.Category, catFilter, StringComparison.OrdinalIgnoreCase)) &&
                (term is null ||
                 p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                 p.Slug.Contains(term, StringComparison.OrdinalIgnoreCase)), ct);

            return Results.Ok(filtered.OrderByDescending(p => p.UpdatedAt).ToList());
        });

        group.MapGet("/products/{id}", async (string id, IDocumentStore<ProductDocument> store, CancellationToken ct) =>
        {
            var doc = await store.GetAsync(id, ct);
            return doc is not null ? Results.Ok(doc) : Results.NotFound();
        });

        group.MapPost("/products", async (
            UpsertProductRequest req,
            IDocumentStore<ProductDocument> store,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            IOptionsMonitor<CatalogOptions> catalogOptions,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Product name is required."] });

            var catOpts = catalogOptions.CurrentValue;
            var slug = string.IsNullOrWhiteSpace(req.Slug)
                ? Slugify(req.Name, catOpts.FallbackSlug)
                : Slugify(req.Slug, catOpts.FallbackSlug);

            var slugCollisionCount = await store.CountAsync(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase), ct);
            if (slugCollisionCount > 0)
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}";

            var rules = await rulesStore.GetRulesAsync(ct);
            var basePrices = NormalizeAndDeriveBasePrices(req.BasePrices, rules, regions, catOpts.DefaultBasePriceMinor);

            var now = DateTimeOffset.UtcNow;
            var doc = new ProductDocument
            {
                Id = $"prd_{Guid.NewGuid():N}"[..16],
                Slug = slug,
                Name = req.Name.Trim(),
                Category = string.IsNullOrWhiteSpace(req.Category) ? catOpts.DefaultCategory : req.Category.Trim(),
                Type = req.Type,
                Status = req.Status,
                ShortDescription = req.ShortDescription?.Trim() ?? "",
                Description = req.Description?.Trim() ?? "",
                Images = req.Images ?? [],
                BasePrices = basePrices,
                Options = req.Options ?? [],
                LeadTimeDays = req.LeadTimeDays > 0 ? req.LeadTimeDays : catOpts.DefaultLeadTimeDays,
                Tags = req.Tags ?? [],
                IsFeatured = req.IsFeatured,
                CreatedAt = now,
                UpdatedAt = now
            };

            var saved = await store.UpsertAsync(doc, ct: ct);
            return Results.Created($"/api/admin/products/{saved.Id}", saved);
        });

        group.MapPut("/products/{id}", async (
            string id,
            UpsertProductRequest req,
            HttpContext http,
            IDocumentStore<ProductDocument> store,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            IOptionsMonitor<CatalogOptions> catalogOptions,
            CancellationToken ct) =>
        {
            var existingTask = store.GetAsync(id, ct);
            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(existingTask, rulesTask);

            var existing = await existingTask;
            if (existing is null) return Results.NotFound();

            int? expectedVersion = req.Version;
            if (http.Request.Headers.TryGetValue("If-Match", out var ifMatch) &&
                int.TryParse(ifMatch.ToString().Trim('"'), out var parsedVer))
            {
                expectedVersion = parsedVer;
            }

            var catOpts = catalogOptions.CurrentValue;
            var rules = await rulesTask;
            existing.Name = req.Name.Trim();
            existing.Slug = string.IsNullOrWhiteSpace(req.Slug) ? existing.Slug : Slugify(req.Slug, catOpts.FallbackSlug);
            existing.Category = string.IsNullOrWhiteSpace(req.Category) ? existing.Category : req.Category.Trim();
            existing.Type = req.Type;
            existing.Status = req.Status;
            existing.ShortDescription = req.ShortDescription?.Trim() ?? "";
            existing.Description = req.Description?.Trim() ?? "";
            existing.Images = req.Images ?? existing.Images;
            existing.BasePrices = NormalizeAndDeriveBasePrices(req.BasePrices, rules, regions, catOpts.DefaultBasePriceMinor);
            existing.Options = req.Options ?? [];
            existing.LeadTimeDays = req.LeadTimeDays > 0 ? req.LeadTimeDays : existing.LeadTimeDays;
            existing.Tags = req.Tags ?? [];
            existing.IsFeatured = req.IsFeatured;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            try
            {
                var saved = await store.UpsertAsync(existing, expectedVersion, ct);
                return Results.Ok(saved);
            }
            catch (ConcurrencyException cex)
            {
                return Results.Problem(title: cex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapPatch("/products/{id}/status", async (
            string id,
            UpdateProductStatusRequest req,
            IDocumentStore<ProductDocument> store,
            RegionResolver regions,
            CancellationToken ct) =>
        {
            var existing = await store.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();

            if (req.Status == ProductStatus.Published)
            {
                var missing = regions.All
                    .Where(r => !existing.BasePrices.TryGetValue(r.Code, out var bp) || bp.Amount <= 0)
                    .Select(r => r.Code)
                    .ToList();
                if (missing.Count > 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["basePrices"] = [$"Cannot publish before setting prices for: {string.Join(", ", missing)}."]
                    });
                }
            }

            existing.Status = req.Status;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            var saved = await store.UpsertAsync(existing, ct: ct);
            return Results.Ok(saved);
        });

        group.MapDelete("/products/{id}", async (
            string id,
            bool? hard,
            IDocumentStore<ProductDocument> store,
            DataRoot dataRoot,
            CancellationToken ct) =>
        {
            var existing = await store.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();

            if (hard == true)
            {
                await store.DeleteAsync(id, ct);
                var prodUploadDir = Path.Combine(dataRoot.UploadsPath, "products", id);
                if (Directory.Exists(prodUploadDir))
                {
                    try { Directory.Delete(prodUploadDir, recursive: true); } catch { /* ignore */ }
                }
                return Results.Ok(new { deleted = true, hard = true });
            }

            existing.Status = ProductStatus.Archived;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            var saved = await store.UpsertAsync(existing, ct: ct);
            return Results.Ok(saved);
        });

        // 3. Product image upload / reorder / delete
        group.MapPost("/products/{id}/images", async (
            string id,
            IFormFile file,
            string? alt,
            IDocumentStore<ProductDocument> store,
            CustomerUploadStore uploadStore,
            DataRoot dataRoot,
            CancellationToken ct) =>
        {
            var product = await store.GetAsync(id, ct);
            if (product is null) return Results.NotFound();
            if (file is null || file.Length == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Image file is required."] });

            byte[] bytes;
            string ext;
            try
            {
                await using var stream = file.OpenReadStream();
                (bytes, ext) = await uploadStore.ReadAndSanitizeAsync(stream, file.Length, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [ex.Message] });
            }

            var imgId = $"img_{Guid.NewGuid():N}"[..12];
            var fileName = $"{imgId}.{ext}";
            var dir = Path.Combine(dataRoot.UploadsPath, "products", product.Id);
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, ct);

            var url = $"/uploads/products/{product.Id}/{fileName}";
            var isFirst = product.Images.Count == 0;
            var img = new ProductImage
            {
                Id = imgId,
                Url = url,
                ThumbUrl = url,
                Alt = string.IsNullOrWhiteSpace(alt) ? product.Name : alt.Trim(),
                SortOrder = product.Images.Count,
                IsPrimary = isFirst
            };
            product.Images.Add(img);
            product.UpdatedAt = DateTimeOffset.UtcNow;

            var saved = await store.UpsertAsync(product, ct: ct);
            return Results.Ok(saved);
        }).DisableAntiforgery();

        group.MapPut("/products/{id}/images/{imageId}/primary", async (
            string id,
            string imageId,
            IDocumentStore<ProductDocument> store,
            CancellationToken ct) =>
        {
            var product = await store.GetAsync(id, ct);
            if (product is null) return Results.NotFound();

            foreach (var img in product.Images)
                img.IsPrimary = string.Equals(img.Id, imageId, StringComparison.OrdinalIgnoreCase);

            product.UpdatedAt = DateTimeOffset.UtcNow;
            var saved = await store.UpsertAsync(product, ct: ct);
            return Results.Ok(saved);
        });

        group.MapDelete("/products/{id}/images/{imageId}", async (
            string id,
            string imageId,
            IDocumentStore<ProductDocument> store,
            CancellationToken ct) =>
        {
            var product = await store.GetAsync(id, ct);
            if (product is null) return Results.NotFound();

            product.Images.RemoveAll(i => string.Equals(i.Id, imageId, StringComparison.OrdinalIgnoreCase));
            if (product.Images.Count > 0 && !product.Images.Any(i => i.IsPrimary))
                product.Images[0].IsPrimary = true;

            product.UpdatedAt = DateTimeOffset.UtcNow;
            var saved = await store.UpsertAsync(product, ct: ct);
            return Results.Ok(saved);
        });

        // 4. Regional pricing matrix, derivation rules & recalculation
        group.MapGet("/pricing/matrix", async (
            IDocumentStore<ProductDocument> products,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            CancellationToken ct) =>
        {
            var productsTask = products.QueryAsync(ct: ct);
            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(productsTask, rulesTask);

            var allProducts = (await productsTask).OrderBy(p => p.Name).ToList();
            return Results.Ok(new PricingMatrixResponse(regions.All, await rulesTask, allProducts));
        });

        group.MapPut("/pricing/matrix", async (
            UpdatePricingMatrixRequest req,
            IDocumentStore<ProductDocument> products,
            RegionResolver regions,
            CancellationToken ct) =>
        {
            var updated = 0;
            foreach (var row in req.Rows)
            {
                var product = await products.GetAsync(row.ProductId, ct);
                if (product is null) continue;

                foreach (var (regCode, priceDto) in row.BasePrices)
                {
                    var reg = regions.Find(regCode);
                    if (reg is null) continue;
                    product.BasePrices[reg.Code] = new RegionalBasePrice
                    {
                        Amount = Math.Max(0, priceDto.Amount),
                        Currency = reg.Currency,
                        IsOverridden = priceDto.IsOverridden
                    };
                }
                product.UpdatedAt = DateTimeOffset.UtcNow;
                await products.UpsertAsync(product, ct: ct);
                updated++;
            }

            return Results.Ok(new { updated });
        });

        group.MapGet("/pricing/rules", async (IDocumentStore<PricingRulesDocument> rulesStore, CancellationToken ct) =>
        {
            var rules = await rulesStore.GetRulesAsync(ct);
            return Results.Ok(rules);
        });

        group.MapPut("/pricing/rules", async (
            PricingRulesDocument incoming,
            IDocumentStore<PricingRulesDocument> rulesStore,
            CancellationToken ct) =>
        {
            incoming.Id = PricingRulesDocument.SingletonId;
            var saved = await rulesStore.UpsertAsync(incoming, ct: ct);
            return Results.Ok(saved);
        });

        group.MapPost("/pricing/recalculate", async (
            RecalculatePricesRequest req,
            IDocumentStore<ProductDocument> products,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            CancellationToken ct) =>
        {
            var rulesTask = rulesStore.GetRulesAsync(ct);
            var productsTask = products.QueryAsync(ct: ct);
            await Task.WhenAll(rulesTask, productsTask);

            var rules = await rulesTask;
            var all = await productsTask;
            var changes = new List<RecalculatedPriceDiff>();

            foreach (var product in all)
            {
                if (!product.BasePrices.TryGetValue(rules.BaseRegion, out var baseInr) || baseInr.Amount <= 0)
                    continue;

                var productChanged = false;
                foreach (var reg in regions.All.Where(r => !string.Equals(r.Code, rules.BaseRegion, StringComparison.OrdinalIgnoreCase)))
                {
                    product.BasePrices.TryGetValue(reg.Code, out var current);
                    if (current is not null && current.IsOverridden && !req.ForceAll)
                        continue;

                    if (!rules.RegionRules.TryGetValue(reg.Code, out var devRule))
                        continue;

                    var derivedAmount = PricingEngine.DeriveAmount(baseInr.Amount, devRule);
                    var oldAmount = current?.Amount ?? 0;

                    if (derivedAmount != oldAmount || current is null)
                    {
                        changes.Add(new RecalculatedPriceDiff(product.Id, product.Name, reg.Code, reg.Currency, oldAmount, derivedAmount));
                        if (!req.DryRun)
                        {
                            product.BasePrices[reg.Code] = new RegionalBasePrice
                            {
                                Amount = derivedAmount,
                                Currency = reg.Currency,
                                IsOverridden = false
                            };
                            productChanged = true;
                        }
                    }
                }

                if (productChanged && !req.DryRun)
                {
                    product.UpdatedAt = DateTimeOffset.UtcNow;
                    await products.UpsertAsync(product, ct: ct);
                }
            }

            return Results.Ok(new RecalculatePricesResponse(req.DryRun, changes.Count, changes));
        });

        group.MapPost("/pricing/preview", async (
            AdminPreviewQuoteRequest req,
            IDocumentStore<ProductDocument> products,
            IDocumentStore<PricingRulesDocument> rulesStore,
            RegionResolver regions,
            PricingEngine engine,
            CancellationToken ct) =>
        {
            var productTask = products.GetAsync(req.ProductId, ct);
            var rulesTask = rulesStore.GetRulesAsync(ct);
            await Task.WhenAll(productTask, rulesTask);

            var product = await productTask;
            if (product is null) return Results.NotFound();

            var rules = await rulesTask;
            var quotes = new Dictionary<string, QuoteResult>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in regions.All)
            {
                if (product.BasePrices.ContainsKey(r.Code))
                {
                    quotes[r.Code] = engine.CalculateQuote(product, rules, r.Code, req.Selections, req.Extras);
                }
            }

            return Results.Ok(quotes);
        });

        // 5. Leads inbox (Contacts + Custom Requests)
        group.MapGet("/leads", async (
            string? status,
            IDocumentStore<ContactDocument> contacts,
            IDocumentStore<CustomRequestDocument> customRequests,
            CancellationToken ct) =>
        {
            LeadStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<LeadStatus>(status.Trim(), ignoreCase: true, out var ls))
            {
                parsedStatus = ls;
            }

            var contactsTask = contacts.QueryAsync(
                parsedStatus.HasValue ? c => c.Status == parsedStatus.Value : null, ct);
            var requestsTask = customRequests.QueryAsync(
                parsedStatus.HasValue ? r => r.Status == parsedStatus.Value : null, ct);
            await Task.WhenAll(contactsTask, requestsTask);

            return Results.Ok(new AdminLeadsResponse(
                Contacts: (await contactsTask).OrderByDescending(c => c.CreatedAt).ToList(),
                CustomRequests: (await requestsTask).OrderByDescending(r => r.CreatedAt).ToList()));
        });

        group.MapPatch("/leads/contacts/{id}", async (
            string id,
            UpdateLeadStatusRequest req,
            IDocumentStore<ContactDocument> contacts,
            CancellationToken ct) =>
        {
            var doc = await contacts.GetAsync(id, ct);
            if (doc is null) return Results.NotFound();

            doc.Status = req.Status;
            if (req.AdminNotes is not null)
                doc.AdminNotes = req.AdminNotes.Trim();

            var saved = await contacts.UpsertAsync(doc, ct: ct);
            return Results.Ok(saved);
        });

        group.MapPatch("/leads/requests/{id}", async (
            string id,
            UpdateLeadStatusRequest req,
            IDocumentStore<CustomRequestDocument> requests,
            CancellationToken ct) =>
        {
            var doc = await requests.GetAsync(id, ct);
            if (doc is null) return Results.NotFound();

            doc.Status = req.Status;
            if (req.AdminNotes is not null)
                doc.AdminNotes = req.AdminNotes.Trim();

            var saved = await requests.UpsertAsync(doc, ct: ct);
            return Results.Ok(saved);
        });

        group.MapDelete("/leads/{kind}/{id}", async (
            string kind,
            string id,
            IDocumentStore<ContactDocument> contacts,
            IDocumentStore<CustomRequestDocument> requests,
            DataRoot dataRoot,
            CancellationToken ct) =>
        {
            if (string.Equals(kind, "contacts", StringComparison.OrdinalIgnoreCase))
            {
                var deleted = await contacts.DeleteAsync(id, ct);
                return deleted ? Results.Ok(new { deleted = true }) : Results.NotFound();
            }
            if (string.Equals(kind, "requests", StringComparison.OrdinalIgnoreCase))
            {
                var deleted = await requests.DeleteAsync(id, ct);
                var reqDir = Path.Combine(dataRoot.UploadsPath, "requests", id);
                if (Directory.Exists(reqDir))
                {
                    try { Directory.Delete(reqDir, recursive: true); } catch { /* ignore */ }
                }
                return deleted ? Results.Ok(new { deleted = true }) : Results.NotFound();
            }
            return Results.BadRequest();
        });

        group.MapGet("/leads/export.csv", async (
            IDocumentStore<ContactDocument> contacts,
            IDocumentStore<CustomRequestDocument> requests,
            CancellationToken ct) =>
        {
            var contactsTask = contacts.QueryAsync(ct: ct);
            var requestsTask = requests.QueryAsync(ct: ct);
            await Task.WhenAll(contactsTask, requestsTask);

            var allContacts = await contactsTask;
            var allRequests = await requestsTask;

            var sb = new StringBuilder();
            sb.AppendLine("Type,Reference,CreatedAt,Status,Name,Phone,Email,Address,SelectedRegion,DetectedRegion,Product,QuotedTotal,Currency,Notes");

            foreach (var c in allContacts.OrderByDescending(x => x.CreatedAt))
            {
                sb.AppendLine(string.Join(",",
                    Csv("Contact"),
                    Csv(c.Reference),
                    Csv(c.CreatedAt.ToString("u")),
                    Csv(c.Status.ToString()),
                    Csv(c.Name),
                    Csv(c.Phone),
                    Csv(c.Email),
                    Csv(c.Address),
                    Csv(c.SelectedRegion),
                    Csv(c.DetectedRegion),
                    Csv(c.ProductName ?? c.ProductSlug),
                    Csv(""),
                    Csv(""),
                    Csv(c.Message)));
            }

            foreach (var r in allRequests.OrderByDescending(x => x.CreatedAt))
            {
                sb.AppendLine(string.Join(",",
                    Csv("CustomRequest"),
                    Csv(r.Reference),
                    Csv(r.CreatedAt.ToString("u")),
                    Csv(r.Status.ToString()),
                    Csv(r.Contact.Name),
                    Csv(r.Contact.Phone),
                    Csv(r.Contact.Email),
                    Csv(r.Contact.Address),
                    Csv(r.SelectedRegion),
                    Csv(r.DetectedRegion),
                    Csv(r.ProductNameSnapshot),
                    Csv((r.Quote.Total / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    Csv(r.Quote.Currency),
                    Csv(r.Notes)));
            }

            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "resinart-leads.csv");
        });

        // 6. Configuration management (appsettings.Business.json)
        group.MapGet("/settings", (SettingsService settings) => Results.Ok(settings.GetCurrent()));

        group.MapPut("/settings", async (
            UpdateSettingsRequest req,
            SettingsService settings,
            CancellationToken ct) =>
        {
            try
            {
                var updated = await settings.UpdateAsync(req, ct);
                return Results.Ok(updated);
            }
            catch (SettingsValidationException vex)
            {
                return Results.ValidationProblem(vex.Errors);
            }
        });

        // 7. Backups & retention maintenance
        group.MapGet("/backups", (DataMaintenanceService maintenance) =>
            Results.Ok(maintenance.ListBackups()));

        group.MapPost("/backups/run", async (DataMaintenanceService maintenance, CancellationToken ct) =>
        {
            var report = await maintenance.RunFullMaintenanceAsync("admin", ct: ct);
            return Results.Ok(report);
        });

        group.MapGet("/backups/{fileName}", (string fileName, DataMaintenanceService maintenance) =>
        {
            var path = maintenance.ResolveBackupFilePath(fileName);
            if (path is null) return Results.NotFound();
            return Results.File(path, "application/zip", Path.GetFileName(path));
        });

        return app;
    }

    private static Dictionary<string, RegionalBasePrice> NormalizeAndDeriveBasePrices(
        Dictionary<string, RegionalBasePrice>? incoming,
        PricingRulesDocument rules,
        RegionResolver regions,
        long defaultBasePriceMinor)
    {
        var result = new Dictionary<string, RegionalBasePrice>(StringComparer.OrdinalIgnoreCase);
        var inrAmount = incoming is not null && incoming.TryGetValue(rules.BaseRegion, out var baseIn)
            ? Math.Max(0, baseIn.Amount)
            : Math.Max(0, defaultBasePriceMinor);

        foreach (var reg in regions.All)
        {
            if (incoming is not null && incoming.TryGetValue(reg.Code, out var provided) && provided.Amount > 0)
            {
                result[reg.Code] = new RegionalBasePrice
                {
                    Amount = provided.Amount,
                    Currency = reg.Currency,
                    IsOverridden = provided.IsOverridden
                };
            }
            else if (string.Equals(reg.Code, rules.BaseRegion, StringComparison.OrdinalIgnoreCase))
            {
                result[reg.Code] = new RegionalBasePrice
                {
                    Amount = inrAmount,
                    Currency = reg.Currency,
                    IsOverridden = true
                };
            }
            else if (rules.RegionRules.TryGetValue(reg.Code, out var devRule))
            {
                result[reg.Code] = new RegionalBasePrice
                {
                    Amount = PricingEngine.DeriveAmount(inrAmount, devRule),
                    Currency = reg.Currency,
                    IsOverridden = false
                };
            }
        }

        return result;
    }

    private static string Slugify(string value, string fallbackSlug = "resin-piece")
    {
        var lower = value.Trim().ToLowerInvariant();
        var slug = NonSlugChars().Replace(lower, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug)
            ? (string.IsNullOrWhiteSpace(fallbackSlug) ? "resin-piece" : fallbackSlug)
            : slug;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}

public sealed record AdminDashboardDto(
    int PublishedProducts,
    int DraftProducts,
    int ArchivedProducts,
    int NewContacts,
    int TotalContacts,
    int NewCustomRequests,
    int TotalCustomRequests,
    int RegionMismatches);

public sealed record UpsertProductRequest(
    string Name,
    string? Slug,
    string? Category,
    ProductType Type,
    ProductStatus Status,
    string? ShortDescription,
    string? Description,
    List<ProductImage>? Images,
    Dictionary<string, RegionalBasePrice>? BasePrices,
    List<ProductOption>? Options,
    int LeadTimeDays,
    List<string>? Tags,
    bool IsFeatured,
    int? Version = null);

public sealed record UpdateProductStatusRequest(ProductStatus Status);

public sealed record PricingMatrixResponse(
    IReadOnlyList<RegionDefinition> Regions,
    PricingRulesDocument Rules,
    IReadOnlyList<ProductDocument> Products);

public sealed record PricingMatrixRowUpdate(
    string ProductId,
    Dictionary<string, RegionalBasePrice> BasePrices);

public sealed record UpdatePricingMatrixRequest(List<PricingMatrixRowUpdate> Rows);

public sealed record RecalculatePricesRequest(bool DryRun, bool ForceAll = false);

public sealed record RecalculatedPriceDiff(
    string ProductId,
    string ProductName,
    string Region,
    string Currency,
    long OldAmount,
    long NewAmount);

public sealed record RecalculatePricesResponse(
    bool DryRun,
    int Count,
    IReadOnlyList<RecalculatedPriceDiff> Changes);

public sealed record AdminPreviewQuoteRequest(
    string ProductId,
    List<OptionSelection>? Selections,
    List<string>? Extras);

public sealed record AdminLeadsResponse(
    IReadOnlyList<ContactDocument> Contacts,
    IReadOnlyList<CustomRequestDocument> CustomRequests);

public sealed record UpdateLeadStatusRequest(LeadStatus Status, string? AdminNotes);
