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
            Name = "Luxury Resin Name Plates",
            Description = "Royal black & gold geode, grey marble with greenery, ocean shell and spiritual house entrance nameplates.",
            SortOrder = 1,
            ImageUrl = "/uploads/seed/grey-marble-ganesha-name-plate.jpg"
        },
        new()
        {
            Id = "cat_wall_watches",
            Slug = "wall-clocks",
            Name = "Memory & Varmala Wall Clocks",
            Description = "Handcrafted resin wall clocks preserving wedding varmala petals, couple portraits and 12-photo family milestones.",
            SortOrder = 2,
            ImageUrl = "/uploads/seed/varmala-preservation-couple-clock.jpg"
        },
        new()
        {
            Id = "cat_divine_pooja",
            Slug = "divine-pooja",
            Name = "Divine Krishna Jhulas & Pooja Art",
            Description = "Hand-poured Laddu Gopal resin swings (jhulas), Bal Ganesha pooja thalis and festive Rakshabandhan hampers.",
            SortOrder = 3,
            ImageUrl = "/uploads/seed/krishna-jhula-royal-blue-1.jpg"
        },
        new()
        {
            Id = "cat_keepsakes",
            Slug = "custom-keepsakes",
            Name = "Engagement Platters & Wedding Frames",
            Description = "Bespoke floral hoop ring ceremony platters and Gujarati wedding kankotri (invitation) preservation frames.",
            SortOrder = 4,
            ImageUrl = "/uploads/seed/engagement-ring-platter-hoop.jpg"
        },
        new()
        {
            Id = "cat_corporate",
            Slug = "corporate-signage",
            Name = "Corporate Signage & Festive Gifts",
            Description = "Custom 3D acrylic & resin brand identity office plaques, tea coasters and international festive gift hampers.",
            SortOrder = 5,
            ImageUrl = "/uploads/seed/top-search-corporate-office-name-plate.jpg"
        }
    ];

    public static List<ProductDocument> BuildDefaultProducts() =>
    [
        // 1. Grey Marble & Greenery Nameplate (Post #1: DfBcXTLoDBL)
        new()
        {
            Id = "prd_marble_greenery_np",
            Slug = "grey-marble-greenery-name-plate",
            Name = "Grey Marble & Lush Greenery Golden Ganesha Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 8,
            ShortDescription = "Grey marble resin plaque with 3D golden lettering, Ganesha & peacock feather motifs, and lush greenery.",
            Description = """
                A grand welcome begins at your door! Inspired by modern luxury architecture, this two-tier grey marble resin nameplate blends natural greenery with 3D mirror-gold lettering.

                - **Upper Plaque:** Hand-poured grey marble resin with 3D golden peacock feather and family names
                - **Planter Divider:** Lifelike UV-resistant artificial boxwood greenery nestled between the tiers
                - **Lower Plaque:** 3D laser-cut golden Lord Ganesha emblem alongside raised house/flat numbers
                - **Original Instagram Post:** `https://www.instagram.com/p/DfBcXTLoDBL/`
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
                    Label = "Plaque Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "16x12", Label = "16 × 12 inch (Standard Two-Tier)" },
                        new() { Id = "20x15", Label = "20 × 15 inch (Grand Entrance)", Modifier = Fixed(90000, 2000, 2600) },
                        new() { Id = "24x18", Label = "24 × 18 inch (Bungalow Foyer)", Modifier = Fixed(180000, 3800, 4900) }
                    ]
                },
                new()
                {
                    Id = "opt_house_text",
                    Label = "Family Name(s) & Flat / House Number",
                    Type = OptionType.Text,
                    Required = true,
                    HelpText = "Enter the exact family name(s) and house/flat number (English, Gujarati or Hindi).",
                    Constraints = new OptionConstraints { MaxLength = 60, Placeholder = "e.g. Mitesh Vinodkumar Soni • 204" }
                },
                new()
                {
                    Id = "opt_led",
                    Label = "Warm Backlit LED Halo Kit",
                    Type = OptionType.Boolean,
                    Required = false,
                    Modifier = Fixed(75000, 1800, 2400)
                }
            ],
            Tags = ["name-plate", "marble", "ganesha", "greenery", "bestseller"]
        },

        // 2. Ocean Magic Sea Shell Nameplate (Post #2: De_WqM6tHEW)
        new()
        {
            Id = "prd_ocean_shell_np",
            Slug = "ocean-magic-shell-name-plate",
            Name = "Ocean Magic Sea Shell & Bansuri Round Resin Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 8,
            ShortDescription = "Scalloped ocean-wave resin nameplate with real sea shells, shore sand, golden Bansuri flute and peacock feather.",
            Description = """
                A little ocean magic crafted to welcome you home. Poured in multi-layer turquoise and deep sapphire ocean waves washing over real beach sand, sea shells, miniature sea turtles and a wooden boat.

                - Adorned with a 3D golden Krishna Bansuri (flute) and peacock feather at the crown
                - Crisp white 3D acrylic family names and flat number
                - Shipped across Pune, Gujarat, all India, USA & Canada
                - **Original Instagram Post:** `https://www.instagram.com/p/De_WqM6tHEW/`
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
                    Label = "Diameter",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "14in", Label = "14 inch Scalloped Round" },
                        new() { Id = "16in", Label = "16 inch Scalloped Round", Modifier = Fixed(60000, 1400, 1800) },
                        new() { Id = "18in", Label = "18 inch Grand Round", Modifier = Fixed(120000, 2600, 3400) }
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

        // 3. Handcrafted Resin Krishna Jhula (Posts #6 & #11: DceEn2xoKAS & DbdhxTJoCP6)
        new()
        {
            Id = "prd_krishna_jhula",
            Slug = "resin-krishna-jhula-swing",
            Name = "Divine Royal Blue & Peacock Feather Resin Krishna Jhula",
            Category = "divine-pooja",
            Type = ProductType.Standard,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 10,
            ShortDescription = "Handcrafted Laddu Gopal resin swing with royal blue & amber swirls, 3D pearl flowers, golden peacocks and real peacock feather.",
            Description = """
                Where devotion meets artistry. Welcome Laddu Gopal into your home mandir with this handcrafted Resin Krishna Jhula (swing) created in Nadiad by Dhara Shah.

                - **Arch & Base:** Deep royal blue, pearl-white and golden-amber swirled resin arch on a crystal-encrusted scalloped base
                - **Embellishments:** Hand-sculpted 3D blue & white blossoms with pearl clusters, golden peacock motifs and a crown of real peacock feathers
                - **Swing:** Brass chain swing with gold-rimmed resin seat suitable for Laddu Gopal idols
                - **Original Instagram Posts:** `https://www.instagram.com/p/DbdhxTJoCP6/` & `https://www.instagram.com/p/DceEn2xoKAS/`
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
                        new() { Id = "grand_13in", Label = "13 inch Grand Arch (for Size 3–5 Laddu Gopal)", Modifier = Fixed(70000, 1600, 2100) }
                    ]
                },
                new()
                {
                    Id = "opt_color_theme",
                    Label = "Resin Colour Palette",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "royal_peacock", Label = "Royal Blue, Amber & Pearl White (Signature)" },
                        new() { Id = "vrindavan_pink", Label = "Lotus Pink, Ivory & 24k Gold Leaf" },
                        new() { Id = "emerald_gold", Label = "Morpankh Emerald & Gold Quartz" }
                    ]
                }
            ],
            Tags = ["krishna-jhula", "laddu-gopal", "janmashtami", "mandir-decor", "bestseller"]
        },

        // 4. Bespoke Floral Hoop Engagement Ring Platter (Post #8: DcJDRtwIzL7)
        new()
        {
            Id = "prd_engagement_platter",
            Slug = "personalised-resin-purse",
            Name = "Bespoke Floral Hoop & Calendar Resin Engagement Ring Platter",
            Category = "custom-keepsakes",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 10,
            ShortDescription = "Scalloped pearl-white resin engagement tray with couple monogram hoop arch, roses, ring boxes and embedded ceremony calendar.",
            Description = """
                A little piece of "forever" — handcrafted in resin. From the first ring exchange to a lifetime of memories, this bespoke engagement platter is designed to make your ring ceremony truly magical.

                - **Base Platter:** Scalloped pearl-ivory resin tray with real dried baby's breath blooms, pearls, gold-leaf rim and embedded ceremony month calendar with a heart on your date
                - **Floral Hoop Arch:** Gold metal ring arch adorned with white roses and a suspended resin couple monogram disc (e.g., "AZ")
                - **Ring Holders:** Includes two vintage brass-and-glass hexagonal ring boxes with pearl beds
                - **Original Instagram Post:** `https://www.instagram.com/p/DcJDRtwIzL7/`
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
                    Label = "Floral & Resin Palette",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "blush_gold", Label = "Ivory Pearl, White Roses & Burgundy Gypsophila" },
                        new() { Id = "ivory_pearl", Label = "Bridal Blush Pink & Gold Leaf" },
                        new() { Id = "midnight_orchid", Label = "Royal Emerald & Champagne Gold", Modifier = Fixed(20000, 400, 600) }
                    ]
                },
                new()
                {
                    Id = "opt_name",
                    Label = "Couple Names, Initials & Engagement Date",
                    Type = OptionType.Text,
                    Required = false,
                    HelpText = "Laser-cut metallic gold script for the suspended hoop monogram and calendar date.",
                    Constraints = new OptionConstraints { MaxLength = 50, Placeholder = "e.g. Aditya ❤ Zeel • 26 April 2026" },
                    Modifier = Fixed(15000, 500, 700)
                },
                new()
                {
                    Id = "opt_photo",
                    Label = "Embed Couple Photo or Invitation Motif",
                    Type = OptionType.Image,
                    Required = false,
                    HelpText = "Upload a photo to seal inside the resin platter or monogram charm.",
                    Constraints = new OptionConstraints { MaxFiles = 1, MaxSizeMb = 5 },
                    Modifier = Fixed(30000, 900, 1200)
                },
                new()
                {
                    Id = "opt_gold_edge",
                    Label = "24k Gold-Leaf Hand-Painted Rim",
                    Type = OptionType.Boolean,
                    Required = false,
                    Modifier = new PriceModifier { Kind = ModifierKind.Percent, Value = 10m }
                }
            ],
            Tags = ["engagement-platter", "ring-ceremony", "wedding", "personalised", "bestseller"]
        },

        // 5. Wedding Varmala & Couple Photo Preservation Wall Clock (Post #13: DZ0AXyloyBi)
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
            ShortDescription = "Handcrafted resin wall clock preserving real wedding varmala rose petals, couple portrait and 3D gold Roman numerals.",
            Description = """
                Preserving Varmala — a perfect blend of love and timeless elegance. This handcrafted resin clock captures your wedding moment forever, where every second tells a story of togetherness.

                - **Outer Rim:** Real preserved wedding garland (varmala) rose petals, pearls and gold leaf cast in crystal-clear UV-resistant resin
                - **Dial & Numerals:** Pearl-ivory resin dial with laser-cut 3D mirror-gold Roman numeral chapter ring and silent sweep gold hands
                - **Centrepiece:** High-resolution couple wedding portrait with 3D golden names and wedding date (e.g. "Dinesh ❤ Maya • 07/03/2026")
                - **Original Instagram Post:** `https://www.instagram.com/p/DZ0AXyloyBi/`
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
                    Label = "Diameter",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "12in", Label = "12 inch (Standard Wall Clock)" },
                        new()
                        {
                            Id = "15in",
                            Label = "15 inch (Living Room Statement)",
                            Modifier = Fixed(80000, 1800, 2400)
                        },
                        new()
                        {
                            Id = "18in",
                            Label = "18 inch (Grand Heirloom Clock)",
                            Modifier = Fixed(160000, 3500, 4600)
                        }
                    ]
                },
                new()
                {
                    Id = "opt_markers",
                    Label = "Hour Marker Style",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "dots", Label = "Minimal Gold Dots" },
                        new() { Id = "roman", Label = "3D Mirror Gold Roman Numerals", Modifier = Fixed(25000, 600, 800) },
                        new() { Id = "arabic", Label = "3D Mirror Gold Arabic Numbers", Modifier = Fixed(25000, 600, 800) }
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

        // 6. "Aviraj" & "I Krupa Bhavan" Royal Black & Gold Geode Nameplate (Posts #4 & #10: Dc7vMwMJz6P & DbzlAlgo8CS)
        new()
        {
            Id = "prd_black_gold_np",
            Slug = "royal-black-gold-geode-name-plate",
            Name = "Royal Black & 24k Gold Quartz Geode Entrance Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = true,
            LeadTimeDays = 9,
            ShortDescription = "Mirror-gloss black resin nameplate with raw gold quartz crystals, 3D gold lettering and traditional Trishul/scroll motifs.",
            Description = """
                Because your entrance deserves nothing less than royal vibes! Custom-crafted in mirror-gloss jet black resin with 3D metallic gold lettering, ornate flourishes and genuine gold-plated quartz crystal clusters.

                - Available in **Vertical Bungalow Pillar** layout (like *"Aviraj — B-1 • Raju Shah & Avinanda Shah"*) or **Horizontal Villa Gate** layout (like *"I Krupa Bhavan — Lalit Parmar & Sangita Parmar"* with golden Trishul emblem)
                - Weather-resistant resin coat with concealed stainless-steel mirror studs included
                - **Original Instagram Posts:** `https://www.instagram.com/p/Dc7vMwMJz6P/` & `https://www.instagram.com/p/DbzlAlgo8CS/`
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
                    Label = "Orientation & Style",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "vertical_aviraj", Label = "Vertical Pillar Plaque with Gold Quartz Corners ('Aviraj' Style)" },
                        new() { Id = "horizontal_krupa", Label = "Horizontal Chamfered Plaque with Trishul/Lotus ('I Krupa Bhavan' Style)" }
                    ]
                },
                new()
                {
                    Id = "opt_size",
                    Label = "Dimensions",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "18x8", Label = "18 × 8 inch (Standard Entrance)" },
                        new() { Id = "22x10", Label = "22 × 10 inch (Bungalow Gate)", Modifier = Fixed(90000, 2000, 2600) }
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

        // 7. "Shri Hari" Devotional Navy & Gold Crystal Round Nameplate (Post #3: DeYwX7coJ3N)
        new()
        {
            Id = "prd_shri_hari_np",
            Slug = "shri-hari-devotional-name-plate",
            Name = "Shri Hari Devotional Navy & Crushed Gold Round Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 9,
            ShortDescription = "Round navy-blue resin nameplate rimmed with crushed gold crystals, Gujarati 'Shri Hari' header, Bansuri, lotus and Kamdhenu cows.",
            Description = """
                More than just a nameplate — it is a sacred welcome filled with blessings, love and positive vibes.

                - Deep midnight-blue resin disc rimmed with a hand-laid border of crushed golden crystals
                - Features sacred **શ્રી હરિ (Shri Hari)** calligraphy, Bansuri flute with peacock feather, pink lotus and twin Kamdhenu cows
                - Customisable with your preferred deity motif (Swaminarayan, Ganeshji, Shrinathji or Om) and script (Gujarati, Hindi or English)
                - **Original Instagram Post:** `https://www.instagram.com/p/DeYwX7coJ3N/`
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
                    Label = "Sacred Motif & Header",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "shri_hari", Label = "શ્રી હરિ (Shri Hari) + Bansuri & Kamdhenu Cows" },
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

        // 8. "Shah's 604" Modern Split-Arch Black & Blush Pink Nameplate (Post #12: DZ9gFTOIqmr)
        new()
        {
            Id = "prd_split_arch_np",
            Slug = "modern-split-arch-name-plate",
            Name = "Modern Split-Arch Black & Blush Rose Statement Name Plate",
            Category = "name-plates",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 7,
            ShortDescription = "Contemporary two-piece half-arch black & blush-rose nameplate with Ganesha line art and raised 3D script.",
            Description = """
                Where elegance meets identity — a statement nameplate that speaks luxury before you even step inside. Originally crafted in our Nadiad studio and shipped to Hyderabad.

                - **Left Arch:** Glossy black half-circle plaque with minimalist Ganesha line art, flowing 3D blush-rose script surname and twin vertical accent bars
                - **Right Arch:** Matching blush-rose half-circle wing displaying bold laser-cut flat/house numbers
                - **Original Instagram Post:** `https://www.instagram.com/p/DZ9gFTOIqmr/`
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
                    Label = "Accent Colour Combination",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "black_blush", Label = "Jet Black & Blush Rose Pink (Signature)" },
                        new() { Id = "black_gold", Label = "Jet Black & Champagne Mirror Gold" },
                        new() { Id = "ivory_gold", Label = "Pearl Ivory & Rose Gold" }
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

        // 9. Custom 12-Photo Family Milestone Wall Clock (Post #15: DZZdt0LI1b3)
        new()
        {
            Id = "prd_family_photo_clock",
            Slug = "family-memories-photo-wall-clock",
            Name = "Time Wrapped in Memories — 12-Photo Family Resin Wall Clock",
            Category = "wall-clocks",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 10,
            ShortDescription = "Ocean-blue & pearl-grey swirled resin clock featuring 12 framed milestone photos around the dial and a central family portrait.",
            Description = """
                Time wrapped in memories — every tick of this clock holds a story, from tiny smiles to cherished family moments.

                - **12 Hour Photo Bezels:** Each hour marker (1 to 12) frames a miniature waterproof family or baby milestone photograph
                - **Central Crushed-Crystal Frame:** Highlighted family portrait bordered with raw gold quartz stones
                - **Custom Callouts:** Includes 3D gold name script (e.g. *"Dhwani"*) plus birth month and day calendar emblems
                - **Original Instagram Post:** `https://www.instagram.com/p/DZZdt0LI1b3/`
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
                    Label = "Clock Diameter",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "14in", Label = "14 inch Round (fits 12 hour photos + center photo)" },
                        new() { Id = "18in", Label = "18 inch Grand Round", Modifier = Fixed(100000, 2200, 2900) }
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

        // 10. Gujarati Kankotri / Wedding Invite Preservation Frame (Post #16: DUGVVOECB8s)
        new()
        {
            Id = "prd_kankotri_frame",
            Slug = "wedding-kankotri-preservation-frame",
            Name = "Sacred Wedding Kankotri (Invitation) Preservation Resin Frame",
            Category = "custom-keepsakes",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 12,
            ShortDescription = "Preserve your Gujarati/Indian wedding invitation (kankotri) in a pearl-white resin slab with silver leaf, pearls and pressed flowers.",
            Description = """
                Preserving sacred wedding details in everlasting resin. Originally commissioned as a heartfelt anniversary gift from wife to husband, this heirloom frame immortalises your wedding kankotri (invitation card).

                - Cast in pearl-swirled epoxy resin with organic deckled metallic edges
                - Embellished with silver-foil shlokas, traditional Dandiya/couple motifs, freshwater-style pearls and real pressed corner blossoms
                - Includes tabletop acrylic/metal stand or wall-mounting hardware
                - **Original Instagram Post:** `https://www.instagram.com/p/DUGVVOECB8s/`
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
                    Label = "Slab Dimensions",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "10x14", Label = "10 × 14 inch Organic Edge Slab (Tabletop + Wall)" },
                        new() { Id = "12x18", Label = "12 × 18 inch Gallery Slab (fits Kankotri + Varmala petals)", Modifier = Fixed(80000, 1800, 2400) }
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

        // 11. Corporate Brand Identity Resin Signage Plaque (Post #14: DZsDlGgocLG)
        new()
        {
            Id = "prd_corporate_signage",
            Slug = "corporate-brand-identity-name-plate",
            Name = "Executive Business & Office Brand Identity Resin Plaque",
            Category = "corporate-signage",
            Type = ProductType.Custom,
            Status = ProductStatus.Published,
            IsFeatured = false,
            LeadTimeDays = 7,
            ShortDescription = "Precision 3D laser-cut logo, service icons, tagline and GST number plaque for offices, clinics and studios.",
            Description = """
                First impressions matter — and this one speaks success. A commercial nameplate is the identity of your brand, the first glimpse of your story, and the beginning of your client's trust.

                - High-gloss pearl-white and crystal-clear resin/acrylic sandwich plaque with polished bevels
                - Raised 3D laser-cut brand logo, service pillars/icons, company tagline and GST/registration number bar
                - Includes 4 heavy-duty brushed stainless-steel wall standoffs
                - **Original Instagram Post:** `https://www.instagram.com/p/DZsDlGgocLG/`
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
                    Label = "Office Plaque Size",
                    Type = OptionType.Select,
                    Required = true,
                    Choices =
                    [
                        new() { Id = "18x14", Label = "18 × 14 inch (Cabin / Studio Entrance)" },
                        new() { Id = "24x18", Label = "24 × 18 inch (Reception Wall Statement)", Modifier = Fixed(120000, 2600, 3400) }
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

        // 12. Bal Ganesha Resin Pooja Thali & Rakshabandhan Hamper (Posts #5, #7, #9: Dcq-Ud4oNbQ, DcdlFZTogI-, DcGypgsodbO)
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
            ShortDescription = "Scalloped pearl-white resin Rakhi & pooja platter featuring adorable Bal Ganesha, twin brass kankavati bowls and optional sweet hamper.",
            Description = """
                A little tray of love, blessings & beautiful memories! Handcrafted with devotion, this scalloped resin pooja dish featuring adorable Bal Ganesha, silver leaf and a pearl cluster brooch is a timeless addition to your festive celebrations.

                - Includes attached twin brass kankavati bowls for kumkum/roli and akshat (rice)
                - Popular with NRI sisters in the **USA & Canada** sending festive Rakshabandhan hampers to brothers in India
                - Upgrade to the complete curated gift hamper (Resin Pooja Dish + Bhai & Bhabhi Rakhis + 12 Assorted Truffle Balls + 6 Choco Muffins in an organza gift basket)
                - **Original Instagram Posts:** `https://www.instagram.com/p/DcGypgsodbO/`, `https://www.instagram.com/p/DcdlFZTogI-/` & `https://www.instagram.com/p/Dcq-Ud4oNbQ/`
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
