using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Features.Regions;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Tests;

public sealed class FoundationTests : IDisposable
{
    private readonly string _tempDir;

    public FoundationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "resinart-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task JsonFileStore_PersistsAtomically_AndEnforcesOptimisticConcurrency()
    {
        var filePath = Path.Combine(_tempDir, "categories.json");
        var store = new JsonFileStore<CategoryDocument>(filePath, NullLogger<JsonFileStore<CategoryDocument>>.Instance);

        var created = await store.UpsertAsync(new CategoryDocument
        {
            Id = "cat_1",
            Slug = "clocks",
            Name = "Clocks"
        });
        Assert.Equal(1, created.Version);

        var updated = await store.UpsertAsync(new CategoryDocument
        {
            Id = "cat_1",
            Slug = "clocks",
            Name = "Resin Clocks"
        }, expectedVersion: 1);
        Assert.Equal(2, updated.Version);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.UpsertAsync(new CategoryDocument { Id = "cat_1", Slug = "clocks", Name = "Stale Write" }, expectedVersion: 1));

        // Reload from disk to verify persistence
        using var reloaded = new JsonFileStore<CategoryDocument>(filePath, NullLogger<JsonFileStore<CategoryDocument>>.Instance);
        var fromDisk = await reloaded.GetAsync("cat_1");
        Assert.NotNull(fromDisk);
        Assert.Equal("Resin Clocks", fromDisk.Name);
        Assert.Equal(2, fromDisk.Version);
    }

    [Fact]
    public async Task JsonFileStore_HandlesConcurrentWritesSafely()
    {
        var filePath = Path.Combine(_tempDir, "concurrent.json");
        using var store = new JsonFileStore<CategoryDocument>(filePath, NullLogger<JsonFileStore<CategoryDocument>>.Instance);

        var tasks = Enumerable.Range(1, 25).Select(i =>
            store.UpsertAsync(new CategoryDocument { Id = $"cat_{i:00}", Slug = $"s-{i}", Name = $"Category {i}" }));

        await Task.WhenAll(tasks);

        var all = await store.QueryAsync();
        Assert.Equal(25, all.Count);
    }

    [Theory]
    [InlineData(23.0225, 72.5714, "GJ-IN", true)]   // Ahmedabad, Gujarat
    [InlineData(19.0760, 72.8777, "GJ-IN", true)]   // Mumbai, India -> maps to GJ-IN
    [InlineData(40.7128, -74.0060, "US", true)]     // New York, USA
    [InlineData(43.6532, -79.3832, "CA", true)]     // Toronto, Canada
    [InlineData(51.5074, -0.1278, "US", false)]     // London, UK -> fallback US
    public void RegionResolver_ResolvesCoordinatesAndCountries(double lat, double lng, string expectedRegion, bool expectedMatched)
    {
        var resolver = CreateResolver();
        var match = resolver.FromCoordinates(lat, lng);

        Assert.Equal(expectedRegion, match.RegionCode);
        Assert.Equal(expectedMatched, match.Matched);
        Assert.Equal("GJ-IN", resolver.FromCountry("IN"));
        Assert.Equal("CA", resolver.FromCountry("ca"));
        Assert.Equal("US", resolver.FromCountry("GB")); // unsupported -> fallback
    }

    [Fact]
    public void PricingEngine_CalculatesRegionalQuote_WithFixedPercentAndRounding()
    {
        var resolver = CreateResolver();
        var engine = new PricingEngine(resolver);
        var rules = DataSeeder.BuildDefaultPricingRules();

        var product = new ProductDocument
        {
            Id = "prd_purse",
            Slug = "purse",
            Name = "Purse",
            Status = ProductStatus.Published,
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 249900, Currency = "INR" },
                ["US"]    = new() { Amount = 5499,   Currency = "USD" },
                ["CA"]    = new() { Amount = 7299,   Currency = "CAD" }
            },
            Options =
            [
                new ProductOption
                {
                    Id = "opt_name",
                    Label = "Custom Name",
                    Type = OptionType.Text,
                    Modifier = new PriceModifier
                    {
                        Kind = ModifierKind.Fixed,
                        Values = new(StringComparer.OrdinalIgnoreCase) { ["GJ-IN"] = 15000, ["US"] = 500, ["CA"] = 700 }
                    }
                },
                new ProductOption
                {
                    Id = "opt_gold",
                    Label = "Gold Rim",
                    Type = OptionType.Boolean,
                    Modifier = new PriceModifier { Kind = ModifierKind.Percent, Value = 10m }
                }
            ]
        };

        var quoteIn = engine.CalculateQuote(
            product,
            rules,
            "GJ-IN",
            [
                new OptionSelection("opt_name", Value: "Dhara"),
                new OptionSelection("opt_gold", Provided: true)
            ],
            extraIds: ["giftwrap"]);

        // Base 249900 + Name 15000 + Giftwrap 15000 = 279900; +10% = 307890 -> rounded to nearest 1000 paise (₹10) = 308000 (₹3,080)
        Assert.Equal("INR", quoteIn.Currency);
        Assert.Equal(308000, quoteIn.Total);
        Assert.True(quoteIn.IsEstimate);

        var quoteUs = engine.CalculateQuote(
            product,
            rules,
            "US",
            [new OptionSelection("opt_name", Value: "Dhara")],
            extraIds: null);

        // Base 5499 + Name 500 = 5999 ($59.99), charm rounding keeps 5999
        Assert.Equal("USD", quoteUs.Currency);
        Assert.Equal(5999, quoteUs.Total);
    }

    private static RegionResolver CreateResolver()
    {
        var opts = new RegionOptions
        {
            Default = "GJ-IN",
            FallbackForUnsupported = "US",
            CountryMap = new(StringComparer.OrdinalIgnoreCase) { ["IN"] = "GJ-IN", ["US"] = "US", ["CA"] = "CA" },
            Definitions =
            [
                new() { Code = "GJ-IN", Name = "Gujarat / India", CountryCode = "IN", Currency = "INR", Locale = "en-IN" },
                new() { Code = "US", Name = "United States", CountryCode = "US", Currency = "USD", Locale = "en-US" },
                new() { Code = "CA", Name = "Canada", CountryCode = "CA", Currency = "CAD", Locale = "en-CA" }
            ],
            GeoRules =
            [
                new() { Label = "Gujarat", Region = "GJ-IN", MinLat = 20.1, MaxLat = 24.7, MinLng = 68.1, MaxLng = 74.5 },
                new() { Label = "India", Region = "GJ-IN", MinLat = 6.5, MaxLat = 35.7, MinLng = 68.0, MaxLng = 97.4 },
                new() { Label = "Southern Ontario & Quebec", Region = "CA", MinLat = 42.0, MaxLat = 49.0, MinLng = -83.5, MaxLng = -71.0 },
                new() { Label = "Canada", Region = "CA", MinLat = 49.0, MaxLat = 83.5, MinLng = -141.0, MaxLng = -52.0 },
                new() { Label = "Contiguous US", Region = "US", MinLat = 24.4, MaxLat = 49.0, MinLng = -125.0, MaxLng = -66.9 }
            ]
        };

        var monitor = new StaticOptionsMonitor<RegionOptions>(opts);
        return new RegionResolver(monitor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
