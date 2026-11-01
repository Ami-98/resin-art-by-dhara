using ResinArt.Api.Features.Catalog;
using ResinArt.Api.Features.Pricing;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Infrastructure.Storage;

/// <summary>
/// Seeds the persistent data root with authentic categories, products, regional pricing rules,
/// and photographs from Dhara Shah's official Instagram (@resin_delights_by_dhara, Nadiad &amp; Uttarsanda, Gujarat).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedIfEmptyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var dataRoot = services.GetRequiredService<DataRoot>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        var categoryStore = services.GetRequiredService<IDocumentStore<CategoryDocument>>();
        var productStore = services.GetRequiredService<IDocumentStore<ProductDocument>>();
        var pricingStore = services.GetRequiredService<IDocumentStore<PricingRulesDocument>>();

        CopySeedImages(env.ContentRootPath, dataRoot.UploadsPath, logger);

        if (await pricingStore.GetAsync(PricingRulesDocument.SingletonId, ct) is null)
        {
            await pricingStore.UpsertAsync(BuildDefaultPricingRules(), ct: ct);
            logger.LogInformation("Seeded default pricing rules.");
        }

        if (await categoryStore.CountAsync(ct: ct) == 0)
        {
            var categories = BuildDefaultCategories();
            foreach (var cat in categories)
                await categoryStore.UpsertAsync(cat, ct: ct);
            logger.LogInformation("Seeded {Count} categories.", categories.Count);
        }

        if (await productStore.CountAsync(ct: ct) == 0)
        {
            var products = BuildDefaultProducts();
            foreach (var product in products)
                await productStore.UpsertAsync(product, ct: ct);
            logger.LogInformation("Seeded {Count} sample products.", products.Count);
        }

        await RemoveRetiredOptionsAsync(productStore, logger, ct);
    }

    /// <summary>
    /// Option ids that have been retired from the catalogue. Products persisted by an earlier seed
    /// (seeding only runs on an empty store) still carry them, so they are stripped on startup.
    /// </summary>
    private static readonly HashSet<string> RetiredOptionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "opt_gold_edge" // "24k Gold-Leaf Hand-Painted Rim" (+10%) on the engagement ring platter
    };

    private static async Task RemoveRetiredOptionsAsync(
        IDocumentStore<ProductDocument> productStore, ILogger logger, CancellationToken ct)
    {
        var affected = await productStore.QueryAsync(
            p => p.Options is not null && p.Options.Any(o => RetiredOptionIds.Contains(o.Id)), ct);

        foreach (var product in affected)
        {
            product.Options = product.Options.Where(o => !RetiredOptionIds.Contains(o.Id)).ToList();
            await productStore.UpsertAsync(product, ct: ct);
            logger.LogInformation("Removed retired option(s) from product {ProductId}.", product.Id);
        }
    }

    private static void CopySeedImages(string contentRoot, string uploadsRoot, ILogger logger)
    {
        var sourceDir = Path.Combine(contentRoot, "SeedData", "images");
        if (!Directory.Exists(sourceDir)) return;

        var destDir = Path.Combine(uploadsRoot, "seed");
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (!File.Exists(destFile))
            {
                File.Copy(file, destFile);
                logger.LogInformation("Copied seed image {File}", Path.GetFileName(file));
            }
        }
    }

    public static PricingRulesDocument BuildDefaultPricingRules() => new()
    {
        Id = PricingRulesDocument.SingletonId,
        BaseRegion = "GJ-IN",
        RegionRules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GJ-IN"] = new RegionDerivationRule
            {
                Rate = 1m,
                MarkupPercent = 0m,
                Rounding = new RoundingRule { Mode = RoundingMode.Nearest, Step = 1000 } // nearest ₹10
            },
            ["US"] = new RegionDerivationRule
            {
                Rate = 83m,
                MarkupPercent = 15m,
                Rounding = new RoundingRule { Mode = RoundingMode.Charm, Ending = 99 }
            },
            ["CA"] = new RegionDerivationRule
            {
                Rate = 61m,
                MarkupPercent = 15m,
                Rounding = new RoundingRule { Mode = RoundingMode.Charm, Ending = 99 }
            }
        },
        Extras =
        [
            new PricingExtra
            {
                Id = "giftwrap",
                Label = "Velvet gift box & handwritten note",
                Modifier = new PriceModifier
                {
                    Kind = ModifierKind.Fixed,
                    Values = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["GJ-IN"] = 15000, // ₹150
                        ["US"] = 600,      // $6.00
                        ["CA"] = 800       // C$8.00
                    }
                }
            },
            new PricingExtra
            {
                Id = "rush",
                Label = "Express crafting (dispatch in 4 days)",
                Modifier = new PriceModifier
                {
                    Kind = ModifierKind.Percent,
                    Value = 20m
                }
            }
        ],
        Shipping = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GJ-IN"] = new ShippingRule
            {
                Mode = ShippingMode.Included,
                Note = "Free insured delivery across Nadiad, Gujarat & all India (4–6 days)"
            },
            ["US"] = new ShippingRule
            {
                Mode = ShippingMode.Included,
                Note = "Tracked air shipping from Nadiad, Gujarat included (10–16 days)"
            },
            ["CA"] = new ShippingRule
            {
                Mode = ShippingMode.Included,
                Note = "Tracked air shipping from Nadiad, Gujarat included (12–18 days)"
            }
        },
        Tax = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GJ-IN"] = new TaxRule { Display = "inclusive", Label = "Inclusive of all taxes (GST)" },
            ["US"] = new TaxRule { Display = "note", Label = "Import duties, if any, are payable on delivery" },
            ["CA"] = new TaxRule { Display = "note", Label = "Import duties / GST, if any, are payable on delivery" }
        }
    };

    public static List<CategoryDocument> BuildDefaultCategories() =>
    [
        new()
        {
            Id = "cat_name_plates",
            Slug = "name-plates",
            Name = "House Name Plates",
            Description = "Black & gold crystal, grey marble with greenery, ocean shell, and spiritual house entrance nameplates.",
            SortOrder = 1,
            ImageUrl = "/uploads/seed/grey-marble-ganesha-name-plate.jpg"
        },
        new()
        {
            Id = "cat_wall_watches",
            Slug = "wall-clocks",
            Name = "Memory & Varmala Wall Clocks",
            Description = "Handmade resin wall clocks preserving wedding varmala petals, couple photos, and 12-photo family memories.",
            SortOrder = 2,
            ImageUrl = "/uploads/seed/varmala-preservation-couple-clock.jpg"
        },
        new()
        {
            Id = "cat_divine_pooja",
            Slug = "divine-pooja",
            Name = "Krishna Jhulas & Pooja Items",
            Description = "Handmade Laddu Gopal resin swings (jhulas), Bal Ganesha pooja thalis, and festive Rakhi gift hampers.",
            SortOrder = 3,
            ImageUrl = "/uploads/seed/krishna-jhula-royal-blue-1.jpg"
        },
        new()
        {
            Id = "cat_keepsakes",
            Slug = "custom-keepsakes",
            Name = "Engagement Platters & Wedding Frames",
            Description = "Custom floral hoop ring platters and Gujarati wedding kankotri (invitation) preservation frames.",
            SortOrder = 4,
            ImageUrl = "/uploads/seed/engagement-ring-platter-hoop.jpg"
        },
        new()
        {
            Id = "cat_corporate",
            Slug = "corporate-signage",
            Name = "Office Name Plates & Gift Hampers",
            Description = "Custom 3D acrylic & resin office nameplates, coasters, and festive gift hampers.",
            SortOrder = 5,
            ImageUrl = "/uploads/seed/top-search-corporate-office-name-plate.jpg"
        }
    ];

    public static List<ProductDocument> BuildDefaultProducts() =>
    [
        // 1. Grey Marble & Greenery Nameplate
        new()
        {
            Id = "prd_marble_greenery_np",
            Slug = "grey-marble-greenery-name-plate",
            Name = "Grey Marble & Greenery Golden Ganesha Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 8,
            ShortDescription = "Grey marble resin nameplate with 3D gold letters, Ganesha & peacock feather design, and green leaves.",
            Description = """
                A warm welcome starts at your front door! This two-level grey marble resin nameplate combines fresh green leaves with shiny 3D gold letters.

                • **Top Section:** Handmade grey marble resin with a 3D golden peacock feather and your family names
                • **Middle Greenery:** Long-lasting artificial green leaves placed neatly between the two sections
                • **Bottom Section:** 3D golden Lord Ganesha symbol alongside your raised house or flat number
                """,
            Images =
            [
                new()
                {
                    Id = "img_marble_np_1",
                    Url = "/uploads/seed/grey-marble-ganesha-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/grey-marble-ganesha-name-plate.jpg",
                    Alt = "Grey marble resin nameplate with golden lettering, Ganesha motif and lush green leaves",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 349900, Currency = "INR", IsOverridden = true }, // ₹3,499
                ["US"]    = new() { Amount = 7999,   Currency = "USD", IsOverridden = true }, // $79.99
                ["CA"]    = new() { Amount = 10499,  Currency = "CAD", IsOverridden = true }  // C$104.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_dimensions",
                    Label = "Nameplate Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "16x12", Label = "16 × 12 inch (Standard)" },
                        new() { Id = "20x15", Label = "20 × 15 inch (Medium)", Modifier = Fixed(90000, 2000, 2600) },
                        new() { Id = "24x18", Label = "24 × 18 inch (Large)", Modifier = Fixed(180000, 3800, 4900) }
                    ]
                },
                new()
                {
                    Id = "opt_house_text",
                    Label = "Family Name(s) & Flat / House Number",
                    Type = OptionType.Text,
                    Required = true,
                    HelpText = "Enter the family name(s) and house/flat number (English, Gujarati, or Hindi).",
                    Constraints = new OptionConstraints { MaxLength = 60, Placeholder = "e.g. Mitesh Vinodkumar Soni • 204" }
                },
                new()
                {
                    Id = "opt_led",
                    Label = "Warm Backlit LED Light",
                    Type = OptionType.Boolean,
                    Required = false,
                    Modifier = Fixed(75000, 1800, 2400)
                }
            ],
            Tags = ["name-plate", "marble", "ganesha", "greenery", "bestseller"]
        },

        // 2. Ocean Magic Sea Shell Nameplate
        new()
        {
            Id = "prd_ocean_shell_np",
            Slug = "ocean-magic-shell-name-plate",
            Name = "Ocean Wave & Sea Shell Round Resin Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 8,
            ShortDescription = "Round ocean-wave resin nameplate with real sea shells, beach sand, golden flute, and peacock feather.",
            Description = """
                Bring calm ocean vibes to your front door. Handmade with layers of blue and white resin waves over real beach sand, sea shells, tiny sea turtles, and a miniature wooden boat.

                • Topped with a 3D golden Krishna Bansuri (flute) and a real peacock feather
                • Clear white 3D raised letters for your family names and flat number
                • Safely packed and shipped across Gujarat, all of India, USA, and Canada
                """,
            Images =
            [
                new()
                {
                    Id = "img_ocean_np_1",
                    Url = "/uploads/seed/ocean-magic-shell-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/ocean-magic-shell-name-plate.jpg",
                    Alt = "Round blue ocean wave resin nameplate with real shells, sand and golden flute",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 319900, Currency = "INR", IsOverridden = true }, // ₹3,199
                ["US"]    = new() { Amount = 7299,   Currency = "USD", IsOverridden = true }, // $72.99
                ["CA"]    = new() { Amount = 9499,   Currency = "CAD", IsOverridden = true }  // C$94.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_size",
                    Label = "Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "14in", Label = "14 inch Round" },
                        new() { Id = "16in", Label = "16 inch Round", Modifier = Fixed(60000, 1400, 1800) },
                        new() { Id = "18in", Label = "18 inch Large Round", Modifier = Fixed(120000, 2600, 3400) }
                    ]
                },
                new()
                {
                    Id = "opt_names",
                    Label = "Flat Number & Family Names",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 70, Placeholder = "e.g. C 1301 • Hrushikesh • Vishruti • Kaveesh" }
                }
            ],
            Tags = ["name-plate", "ocean", "shells", "bansuri", "featured"]
        },

        // 3. Handcrafted Resin Krishna Jhula
        new()
        {
            Id = "prd_krishna_jhula",
            Slug = "resin-krishna-jhula-swing",
            Name = "Royal Blue & Peacock Feather Resin Krishna Jhula",
            Category = "divine-pooja",
            Type = ProductType.Standard,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 10,
            ShortDescription = "Handmade Laddu Gopal resin swing with royal blue & gold swirls, pearl flowers, golden peacocks, and a real peacock feather.",
            Description = """
                Welcome Laddu Gopal into your home mandir with this handmade Resin Krishna Jhula (swing), made with care in Nadiad by Dhara Shah.

                • **Arch & Base:** Deep royal blue, pearl-white, and gold swirled resin arch on a sturdy base with crystal edges
                • **Details:** Handmade 3D blue and white flowers with pearls, golden peacock designs, and real peacock feathers on top
                • **Swing Seat:** Brass chain swing with a gold-rimmed resin seat made for Laddu Gopal idols
                """,
            Images =
            [
                new()
                {
                    Id = "img_jhula_1",
                    Url = "/uploads/seed/krishna-jhula-royal-blue-1.jpg",
                    ThumbUrl = "/uploads/seed/krishna-jhula-royal-blue-1.jpg",
                    Alt = "Handcrafted royal blue and gold resin Krishna Jhula with peacock feathers and pearl flowers",
                    SortOrder = 0,
                    IsPrimary = true
                },
                new()
                {
                    Id = "img_jhula_2",
                    Url = "/uploads/seed/krishna-jhula-royal-blue-2.jpg",
                    ThumbUrl = "/uploads/seed/krishna-jhula-royal-blue-2.jpg",
                    Alt = "Laddu Gopal seated on royal blue resin Krishna swing with golden peacock accents",
                    SortOrder = 1,
                    IsPrimary = false
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 279900, Currency = "INR", IsOverridden = true }, // ₹2,799
                ["US"]    = new() { Amount = 6499,   Currency = "USD", IsOverridden = true }, // $64.99
                ["CA"]    = new() { Amount = 8499,   Currency = "CAD", IsOverridden = true }  // C$84.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_jhula_size",
                    Label = "Jhula Height & Idol Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "std_10in", Label = "10 inch Arch (for Size 0–2 Laddu Gopal)" },
                        new() { Id = "grand_13in", Label = "13 inch Large Arch (for Size 3–5 Laddu Gopal)", Modifier = Fixed(70000, 1600, 2100) }
                    ]
                },
                new()
                {
                    Id = "opt_color_theme",
                    Label = "Color Theme",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "royal_peacock", Label = "Royal Blue, Gold & Pearl White (Most Popular)" },
                        new() { Id = "vrindavan_pink", Label = "Lotus Pink, Ivory & Gold" },
                        new() { Id = "emerald_gold", Label = "Peacock Green & Gold" }
                    ]
                }
            ],
            Tags = ["krishna-jhula", "laddu-gopal", "janmashtami", "mandir-decor", "bestseller"]
        },

        // 4. Custom Floral Hoop Engagement Ring Platter
        new()
        {
            Id = "prd_engagement_platter",
            Slug = "personalised-resin-purse",
            Name = "Custom Floral Hoop & Calendar Resin Engagement Ring Platter",
            Category = "custom-keepsakes",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 10,
            ShortDescription = "Pearl-white resin engagement ring tray with a flower hoop, couple initials, ring boxes, and your special date calendar.",
            Description = """
                Make your ring ceremony extra special with a handmade resin engagement tray personalized with your names and date.

                • **Base Tray:** Pearl-white resin tray with real dried baby's breath flowers, pearls, gold edges, and a mini calendar marking your engagement date with a heart
                • **Flower Hoop Arch:** Gold metal ring arch decorated with white roses and a hanging resin circle with the couple's initials (like "AZ")
                • **Ring Holders:** Includes two glass-and-brass ring boxes filled with pearls
                """,
            Images =
            [
                new()
                {
                    Id = "img_eng_1",
                    Url = "/uploads/seed/engagement-ring-platter-hoop.jpg",
                    ThumbUrl = "/uploads/seed/engagement-ring-platter-hoop.jpg",
                    Alt = "Pearl white resin engagement ring platter with floral hoop arch, monogram and glass ring boxes",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 249900, Currency = "INR", IsOverridden = true }, // ₹2,499
                ["US"]    = new() { Amount = 5499,   Currency = "USD", IsOverridden = true }, // $54.99
                ["CA"]    = new() { Amount = 7299,   Currency = "CAD", IsOverridden = true }  // C$72.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_palette",
                    Label = "Flower & Resin Colors",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "blush_gold", Label = "Ivory Pearl, White Roses & Baby's Breath Flowers" },
                        new() { Id = "ivory_pearl", Label = "Soft Blush Pink & Gold" },
                        new() { Id = "midnight_orchid", Label = "Emerald Green & Gold", Modifier = Fixed(20000, 400, 600) }
                    ]
                },
                new()
                {
                    Id = "opt_name",
                    Label = "Couple Names, Initials & Engagement Date",
                    Type = OptionType.Text,
                    Required = false,
                    HelpText = "Gold letters for the hanging initials charm and calendar date.",
                    Constraints = new OptionConstraints { MaxLength = 50, Placeholder = "e.g. Aditya ❤ Zeel • 26 April 2026" },
                    Modifier = Fixed(15000, 500, 700)
                },
                new()
                {
                    Id = "opt_photo",
                    Label = "Add a Couple Photo or Invite Design",
                    Type = OptionType.Image,
                    Required = false,
                    HelpText = "Upload a photo to place inside the resin tray or charm.",
                    Constraints = new OptionConstraints { MaxFiles = 1, MaxSizeMb = 5 },
                    Modifier = Fixed(30000, 900, 1200)
                }
            ],
            Tags = ["engagement-platter", "ring-ceremony", "wedding", "personalised", "bestseller"]
        },

        // 5. Wedding Varmala & Couple Photo Preservation Wall Clock
        new()
        {
            Id = "prd_ocean_clock",
            Slug = "ocean-wave-wall-clock",
            Name = "Wedding Varmala & Couple Portrait Resin Wall Clock",
            Category = "wall-clocks",
            Type = ProductType.Standard,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 14,
            ShortDescription = "Handmade resin wall clock preserving your real wedding varmala rose petals, couple photo, and 3D gold numbers.",
            Description = """
                Keep your wedding flowers and favorite couple photo safe forever inside a functional wall clock.

                • **Outer Border:** Real dried wedding garland (varmala) rose petals, pearls, and gold flakes sealed in clear resin
                • **Clock Dial & Hands:** Pearl-white center with 3D gold numbers and silent-sweep gold clock hands (no ticking sound)
                • **Center Photo:** Your wedding photo with 3D golden names and wedding date (like "Dinesh ❤ Maya • 07/03/2026")
                """,
            Images =
            [
                new()
                {
                    Id = "img_varmala_clock_1",
                    Url = "/uploads/seed/varmala-preservation-couple-clock.jpg",
                    ThumbUrl = "/uploads/seed/varmala-preservation-couple-clock.jpg",
                    Alt = "Preserved wedding varmala rose petals and couple portrait resin wall clock with gold Roman numerals",
                    SortOrder = 0,
                    IsPrimary = true
                },
                new()
                {
                    Id = "img_family_clock_2",
                    Url = "/uploads/seed/family-memories-photo-clock.jpg",
                    ThumbUrl = "/uploads/seed/family-memories-photo-clock.jpg",
                    Alt = "Custom family milestone memories resin wall clock with gold hands",
                    SortOrder = 1,
                    IsPrimary = false
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 289900, Currency = "INR", IsOverridden = true }, // ₹2,899
                ["US"]    = new() { Amount = 6499,   Currency = "USD", IsOverridden = true }, // $64.99
                ["CA"]    = new() { Amount = 8499,   Currency = "CAD", IsOverridden = true }  // C$84.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_size",
                    Label = "Clock Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "12in", Label = "12 inch (Standard Wall Clock)" },
                        new()
                        {
                            Id = "15in",
                            Label = "15 inch (Medium Living Room Size)",
                            Modifier = Fixed(80000, 1800, 2400)
                        },
                        new()
                        {
                            Id = "18in",
                            Label = "18 inch (Large Wall Clock)",
                            Modifier = Fixed(160000, 3500, 4600)
                        }
                    ]
                },
                new()
                {
                    Id = "opt_markers",
                    Label = "Number Style",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "dots", Label = "Simple Gold Dots" },
                        new() { Id = "roman", Label = "3D Gold Roman Numerals (I, II, III)", Modifier = Fixed(25000, 600, 800) },
                        new() { Id = "arabic", Label = "3D Gold Standard Numbers (1, 2, 3)", Modifier = Fixed(25000, 600, 800) }
                    ]
                },
                new()
                {
                    Id = "opt_engraving",
                    Label = "Couple Names & Wedding Date",
                    Type = OptionType.Text,
                    Required = false,
                    HelpText = "Up to 40 characters — e.g. Dinesh ❤ Maya • 07/03/2026",
                    Constraints = new OptionConstraints { MaxLength = 40, Placeholder = "e.g. Dinesh ❤ Maya • 07/03/2026" },
                    Modifier = Fixed(20000, 500, 700)
                }
            ],
            Tags = ["wall-clock", "varmala", "wedding", "couple-gift", "bestseller"]
        },

        // 6. Black & Gold Crystal Entrance Nameplate
        new()
        {
            Id = "prd_black_gold_np",
            Slug = "royal-black-gold-geode-name-plate",
            Name = "Black & Gold Crystal Entrance Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 9,
            ShortDescription = "Glossy black resin nameplate with gold quartz crystals, 3D gold letters, and traditional motifs.",
            Description = """
                Make your home entrance stand out with this glossy black and gold resin nameplate. Handmade with 3D gold letters, decorative borders, and real gold-tinted quartz crystal corners.

                • Available in a vertical pillar layout (like "Aviraj — B-1 • Raju Shah & Avinanda Shah") or a horizontal gate layout (like "I Krupa Bhavan — Lalit Parmar & Sangita Parmar" with a golden Trishul symbol)
                • Weather-resistant glossy coat with hidden stainless-steel wall studs included for easy hanging
                """,
            Images =
            [
                new()
                {
                    Id = "img_blk_gold_1",
                    Url = "/uploads/seed/aviraj-black-gold-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/aviraj-black-gold-name-plate.jpg",
                    Alt = "Aviraj vertical black and gold resin nameplate with gold quartz crystal corners",
                    SortOrder = 0,
                    IsPrimary = true
                },
                new()
                {
                    Id = "img_blk_gold_2",
                    Url = "/uploads/seed/krupa-bhavan-black-gold-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/krupa-bhavan-black-gold-name-plate.jpg",
                    Alt = "I Krupa Bhavan horizontal black and gold resin nameplate with golden Trishul motif",
                    SortOrder = 1,
                    IsPrimary = false
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 329900, Currency = "INR", IsOverridden = true }, // ₹3,299
                ["US"]    = new() { Amount = 7499,   Currency = "USD", IsOverridden = true }, // $74.99
                ["CA"]    = new() { Amount = 9899,   Currency = "CAD", IsOverridden = true }  // C$98.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_orientation",
                    Label = "Shape & Style",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "vertical_aviraj", Label = "Vertical Pillar Style with Gold Crystal Corners ('Aviraj' Style)" },
                        new() { Id = "horizontal_krupa", Label = "Horizontal Style with Trishul / Lotus ('I Krupa Bhavan' Style)" }
                    ]
                },
                new()
                {
                    Id = "opt_size",
                    Label = "Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "18x8", Label = "18 × 8 inch (Standard Entrance)" },
                        new() { Id = "22x10", Label = "22 × 10 inch (Large Gate)", Modifier = Fixed(90000, 2000, 2600) }
                    ]
                },
                new()
                {
                    Id = "opt_house_text",
                    Label = "House Name, Number & Family Member Names",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 80, Placeholder = "e.g. B-1 • Aviraj • Raju Shah & Avinanda Shah" }
                }
            ],
            Tags = ["name-plate", "black-and-gold", "royal", "vastu", "featured"]
        },

        // 7. "Shri Hari" Devotional Navy & Gold Crystal Round Nameplate
        new()
        {
            Id = "prd_shri_hari_np",
            Slug = "shri-hari-devotional-name-plate",
            Name = "Shri Hari Navy & Gold Crystal Round Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 9,
            ShortDescription = "Round navy-blue resin nameplate with a crushed gold crystal border, Gujarati 'Shri Hari' text, flute, lotus, and Kamdhenu cows.",
            Description = """
                Welcome guests with blessings and positive energy with this traditional round nameplate.

                • Deep navy-blue resin circle bordered with hand-placed crushed golden crystals
                • Includes sacred શ્રી હરિ (Shri Hari) lettering, Bansuri flute with peacock feather, pink lotus, and two Kamdhenu cows
                • Can be customized with your choice of spiritual symbol (Swaminarayan, Ganeshji, Shrinathji, or Om) and language (Gujarati, Hindi, or English)
                """,
            Images =
            [
                new()
                {
                    Id = "img_shri_hari_1",
                    Url = "/uploads/seed/shri-hari-spiritual-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/shri-hari-spiritual-name-plate.jpg",
                    Alt = "Round navy blue resin nameplate with Shri Hari Gujarati text, Kamdhenu cows and crushed gold crystal border",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 339900, Currency = "INR", IsOverridden = true }, // ₹3,399
                ["US"]    = new() { Amount = 7699,   Currency = "USD", IsOverridden = true }, // $76.99
                ["CA"]    = new() { Amount = 9999,   Currency = "CAD", IsOverridden = true }  // C$99.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_deity",
                    Label = "Spiritual Symbol & Header",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "shri_hari", Label = "શ્રી હરિ (Shri Hari) + Flute & Kamdhenu Cows" },
                        new() { Id = "shri_ganesh", Label = "શ્રી ગણેશાય નમઃ (Lord Ganesha) + Lotus" },
                        new() { Id = "shrinathji", Label = "શ્રીનાથજી (Shrinathji) + Kamdhenu Cows" }
                    ]
                },
                new()
                {
                    Id = "opt_names",
                    Label = "Surname, Couple Names & Flat Number",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 70, Placeholder = "e.g. Soni's • Sweta & Pratik • E-603" }
                }
            ],
            Tags = ["name-plate", "spiritual", "gujarati", "vastu", "housewarming"]
        },

        // 8. "Shah's 604" Modern Split-Arch Black & Blush Pink Nameplate
        new()
        {
            Id = "prd_split_arch_np",
            Slug = "modern-split-arch-name-plate",
            Name = "Modern Split-Arch Black & Pink Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 7,
            ShortDescription = "Modern two-piece half-arch black & soft pink nameplate with Ganesha line art and raised 3D letters.",
            Description = """
                A clean, modern two-piece arch nameplate that looks great outside apartments and homes.

                • **Left Arch:** Glossy black half-circle with simple Ganesha line art and raised 3D family surname
                • **Right Arch:** Matching soft rose-pink half-circle with bold 3D flat or house numbers
                """,
            Images =
            [
                new()
                {
                    Id = "img_arch_np_1",
                    Url = "/uploads/seed/shahs-blush-arch-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/shahs-blush-arch-name-plate.jpg",
                    Alt = "Black and blush pink split-arch nameplate reading Shah's 604 with Ganesha motif",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 269900, Currency = "INR", IsOverridden = true }, // ₹2,699
                ["US"]    = new() { Amount = 5999,   Currency = "USD", IsOverridden = true }, // $59.99
                ["CA"]    = new() { Amount = 7899,   Currency = "CAD", IsOverridden = true }  // C$78.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_accent_color",
                    Label = "Color Combination",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "black_blush", Label = "Black & Soft Rose Pink (Most Popular)" },
                        new() { Id = "black_gold", Label = "Black & Gold" },
                        new() { Id = "ivory_gold", Label = "Pearl White & Rose Gold" }
                    ]
                },
                new()
                {
                    Id = "opt_text",
                    Label = "Family Surname & Flat Number",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 40, Placeholder = "e.g. Shah's • 604" }
                }
            ],
            Tags = ["name-plate", "modern", "minimalist", "apartment"]
        },

        // 9. Custom 12-Photo Family Milestone Wall Clock
        new()
        {
            Id = "prd_family_photo_clock",
            Slug = "family-memories-photo-wall-clock",
            Name = "12-Photo Family Memories Resin Wall Clock",
            Category = "wall-clocks",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 10,
            ShortDescription = "Blue & pearl-grey swirled resin wall clock with 12 small photos around the numbers and a larger family photo in the center.",
            Description = """
                Turn your favorite baby or family photos into a custom wall clock you will love looking at every day.

                • **12 Hour Photos:** Each hour spot (1 to 12) holds a mini waterproof family or baby milestone photo
                • **Center Frame:** A larger family photo in the middle, bordered with gold quartz stones
                • **Personal Details:** Includes a 3D gold name (like "Dhwani") and small birth month/day tags
                """,
            Images =
            [
                new()
                {
                    Id = "img_fam_clock_1",
                    Url = "/uploads/seed/family-memories-photo-clock.jpg",
                    ThumbUrl = "/uploads/seed/family-memories-photo-clock.jpg",
                    Alt = "Blue and grey resin wall clock with 12 framed baby and family photos around the dial",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 349900, Currency = "INR", IsOverridden = true }, // ₹3,499
                ["US"]    = new() { Amount = 7899,   Currency = "USD", IsOverridden = true }, // $78.99
                ["CA"]    = new() { Amount = 10299,  Currency = "CAD", IsOverridden = true }  // C$102.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_clock_size",
                    Label = "Clock Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "14in", Label = "14 inch Round (fits 12 hour photos + center photo)" },
                        new() { Id = "18in", Label = "18 inch Large Round", Modifier = Fixed(100000, 2200, 2900) }
                    ]
                },
                new()
                {
                    Id = "opt_child_name",
                    Label = "Name & Special Month / Day to Highlight",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 50, Placeholder = "e.g. Dhwani • May • Friday" }
                }
            ],
            Tags = ["wall-clock", "photo-clock", "family-gift", "birthday"]
        },

        // 10. Gujarati Kankotri / Wedding Invite Preservation Frame
        new()
        {
            Id = "prd_kankotri_frame",
            Slug = "wedding-kankotri-preservation-frame",
            Name = "Wedding Kankotri (Invitation Card) Resin Frame",
            Category = "custom-keepsakes",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 12,
            ShortDescription = "Preserve your wedding invitation card (kankotri) in a pearl-white resin frame with silver flakes, pearls, and dried flowers.",
            Description = """
                Keep your wedding invitation card (kankotri) safe forever inside a clear, glossy resin frame — a wonderful wedding or anniversary gift.

                • Poured in pearl-white resin with metallic gold or silver edges
                • Decorated with traditional wedding symbols, pearls, and real dried flowers in the corners
                • Comes with a tabletop stand or wall-hanging hardware
                """,
            Images =
            [
                new()
                {
                    Id = "img_kankotri_1",
                    Url = "/uploads/seed/gujarati-kankotri-wedding-frame.jpg",
                    ThumbUrl = "/uploads/seed/gujarati-kankotri-wedding-frame.jpg",
                    Alt = "Gujarati wedding kankotri invitation preserved in white pearl resin frame with pressed flowers",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 259900, Currency = "INR", IsOverridden = true }, // ₹2,599
                ["US"]    = new() { Amount = 5899,   Currency = "USD", IsOverridden = true }, // $58.99
                ["CA"]    = new() { Amount = 7699,   Currency = "CAD", IsOverridden = true }  // C$76.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_frame_size",
                    Label = "Frame Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "10x14", Label = "10 × 14 inch Frame (Tabletop + Wall)" },
                        new() { Id = "12x18", Label = "12 × 18 inch Large Frame (fits Kankotri + Varmala petals)", Modifier = Fixed(80000, 1800, 2400) }
                    ]
                },
                new()
                {
                    Id = "opt_couple_details",
                    Label = "Couple Names & Wedding Date",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 60, Placeholder = "e.g. चि. દુર્મિલ અને ચિ. પ્રાચી" }
                }
            ],
            Tags = ["kankotri", "wedding-invite", "anniversary", "resin-frame", "custom"]
        },

        // 11. Corporate Brand Identity Resin Signage Plaque
        new()
        {
            Id = "prd_corporate_signage",
            Slug = "corporate-brand-identity-name-plate",
            Name = "Business & Office Logo Resin Name Plate",
            Category = "corporate-signage",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 7,
            ShortDescription = "3D laser-cut logo, service icons, tagline, and GST number board for offices, clinics, and studios.",
            Description = """
                Give your office or clinic entrance a clean, professional look. This custom nameplate displays your business logo and details clearly for every visitor.

                • Glossy pearl-white and clear resin/acrylic board with smooth edges
                • Raised 3D laser-cut company logo, service icons, tagline, and GST or registration number
                • Comes with 4 strong stainless-steel wall studs for easy mounting
                """,
            Images =
            [
                new()
                {
                    Id = "img_corp_1",
                    Url = "/uploads/seed/top-search-corporate-office-name-plate.jpg",
                    ThumbUrl = "/uploads/seed/top-search-corporate-office-name-plate.jpg",
                    Alt = "Top Search Digital Marketing Agency 3D acrylic and resin office signage plaque",
                    SortOrder = 0,
                    IsPrimary = true
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 299900, Currency = "INR", IsOverridden = true }, // ₹2,999
                ["US"]    = new() { Amount = 6899,   Currency = "USD", IsOverridden = true }, // $68.99
                ["CA"]    = new() { Amount = 8999,   Currency = "CAD", IsOverridden = true }  // C$89.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_sign_size",
                    Label = "Board Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "18x14", Label = "18 × 14 inch (Cabin / Door Entrance)" },
                        new() { Id = "24x18", Label = "24 × 18 inch (Reception Wall)", Modifier = Fixed(120000, 2600, 3400) }
                    ]
                },
                new()
                {
                    Id = "opt_company_details",
                    Label = "Company Name, Tagline & GST / Reg No.",
                    Type = OptionType.Text,
                    Required = true,
                    Constraints = new OptionConstraints { MaxLength = 100, Placeholder = "e.g. TOP SEARCH Digital Marketing Agency • We Search. You Grow." }
                }
            ],
            Tags = ["corporate", "office-signage", "brand-identity", "name-plate"]
        },

        // 12. Bal Ganesha Resin Pooja Thali & Rakshabandhan Hamper
        new()
        {
            Id = "prd_ganesha_pooja_thali",
            Slug = "bal-ganesha-pooja-thali-hamper",
            Name = "Bal Ganesha Resin Pooja Dish & Festive Rakhi Hamper",
            Category = "divine-pooja",
            Type = ProductType.Standard,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 5,
            ShortDescription = "Pearl-white resin Rakhi & pooja tray with cute Bal Ganesha, two brass bowls, and an optional chocolate gift hamper.",
            Description = """
                A sweet handmade pooja tray for festivals and family celebrations, featuring cute Bal Ganesha, silver flakes, and pearl details.

                • Includes two attached small brass bowls (kankavati) for kumkum and rice
                • A favorite among sisters in the USA & Canada sending Rakshabandhan gifts to brothers in India
                • Option to upgrade to the full gift hamper (Resin Pooja Dish + Bhai & Bhabhi Rakhis + 12 Truffle Balls + 6 Choco Muffins in a gift basket)
                """,
            Images =
            [
                new()
                {
                    Id = "img_thali_1",
                    Url = "/uploads/seed/bal-ganesha-rakhi-pooja-thali.jpg",
                    ThumbUrl = "/uploads/seed/bal-ganesha-rakhi-pooja-thali.jpg",
                    Alt = "Handcrafted white and silver resin pooja thali featuring Bal Ganesha and twin brass bowls",
                    SortOrder = 0,
                    IsPrimary = true
                },
                new()
                {
                    Id = "img_thali_2",
                    Url = "/uploads/seed/rakshabandhan-resin-sweet-hamper-1.jpg",
                    ThumbUrl = "/uploads/seed/rakshabandhan-resin-sweet-hamper-1.jpg",
                    Alt = "Curated Rakshabandhan gift hamper with resin pooja dish, rakhis and chocolates",
                    SortOrder = 1,
                    IsPrimary = false
                },
                new()
                {
                    Id = "img_thali_3",
                    Url = "/uploads/seed/rakshabandhan-resin-sweet-hamper-2.jpg",
                    ThumbUrl = "/uploads/seed/rakshabandhan-resin-sweet-hamper-2.jpg",
                    Alt = "Festive Rakhi hampers ordered from USA for brothers in India",
                    SortOrder = 2,
                    IsPrimary = false
                }
            ],
            BasePrices = new(StringComparer.OrdinalIgnoreCase)
            {
                ["GJ-IN"] = new() { Amount = 99900, Currency = "INR", IsOverridden = true },  // ₹999
                ["US"]    = new() { Amount = 2699,  Currency = "USD", IsOverridden = true },  // $26.99
                ["CA"]    = new() { Amount = 3499,  Currency = "CAD", IsOverridden = true }   // C$34.99
            },
            Options =
            [
                new()
                {
                    Id = "opt_pack",
                    Label = "Package Selection",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "dish_only", Label = "Bal Ganesha Resin Pooja Dish with Twin Brass Bowls" },
                        new()
                        {
                            Id = "full_hamper",
                            Label = "Complete Festive Hamper (Resin Dish + Rakhis + 12 Truffles + 6 Muffins)",
                            Modifier = Fixed(120000, 2500, 3200)
                        }
                    ]
                }
            ],
            Tags = ["pooja-thali", "ganesha", "rakshabandhan", "hamper", "festive-gift"]
        }
    ];

    private static PriceModifier Fixed(long inrPaise, long usdCents, long cadCents) => new()
    {
        Kind = ModifierKind.Fixed,
        Derived = false,
        Values = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GJ-IN"] = inrPaise,
            ["US"] = usdCents,
            ["CA"] = cadCents
        }
    };
}
