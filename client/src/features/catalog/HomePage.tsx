import React from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Globe2, HeartHandshake, MessageCircle, Sparkles } from 'lucide-react';
import { api } from '../../api/client';
import { GoldScrollDivider, RoseWatermarkBadge } from '../../components/BrandLogo';
import { useRegion } from '../region/RegionContext';
import { ProductCard } from './ProductCard';

export const HomePage: React.FC = () => {
  const { region, bootstrap } = useRegion();

  const instagramUrl =
    bootstrap?.business.social?.instagram || 'https://www.instagram.com/resin_delights_by_dhara/';
  const instagramHandle =
    instagramUrl
      .replace(/\/+$/, '')
      .split('/')
      .pop()
      ?.replace(/^@/, '') || 'resin_delights_by_dhara';

  const { data: categories } = useQuery({
    queryKey: ['categories'],
    queryFn: api.getCategories,
    staleTime: 5 * 60 * 1000,
  });

  const { data: featured } = useQuery({
    queryKey: ['products', region, 'featured'],
    queryFn: () => api.getProducts({ region, featured: true }),
  });

  return (
    <div>
      {/* Hero section */}
      <section className="bg-hero-pearl relative overflow-hidden border-b border-[#EADCE0]">
        <div className="mx-auto grid max-w-7xl items-center gap-10 px-4 py-12 sm:px-6 lg:grid-cols-12 lg:py-20 lg:px-8">
          <div className="lg:col-span-7">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-[#D98A9E]/60 bg-[#F6EEF0]/90 px-3.5 py-1 text-xs font-bold text-[#5E3758] shadow-2xs">
              <Sparkles className="h-3.5 w-3.5 text-[#9A7020]" /> Handmade in Nadiad, Gujarat · Shipping Worldwide 🌍
            </span>

            <h1 className="mt-4 text-4xl font-bold leading-tight text-[#161118] sm:text-5xl lg:text-[3.4rem]">
              Turn Your Special <span className="italic text-[#5E3758]">Memories</span> into Handmade Resin Art
            </h1>

            <p className="mt-4 max-w-xl text-base leading-relaxed text-[#4A3E47]">
              From black-and-gold house nameplates and Laddu Gopal swings (jhulas) to wedding
              garland (varmala) wall clocks, engagement ring platters, and wedding invite frames —
              handmade just for you by Dhara Shah (
              <a
                href={instagramUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="font-bold text-[#5E3758] underline decoration-[#D98A9E] underline-offset-2 transition hover:text-[#9A7020]"
              >
                @{instagramHandle}
              </a>
              ).
            </p>

            <div className="mt-5">
              <RoseWatermarkBadge />
            </div>

            <div className="mt-7 flex flex-wrap items-center gap-3.5">
              <Link
                to="/products"
                className="inline-flex items-center gap-2 rounded-full border border-[#C99B3B]/60 bg-obsidian-luxe px-6 py-3.5 text-sm font-bold text-[#FAF7F4] shadow-luxe transition hover:border-[#F3E5AB] hover:text-[#F3E5AB]"
              >
                Shop All Designs <ArrowRight className="h-4 w-4 text-[#C99B3B]" />
              </Link>
              <Link
                to="/custom"
                className="inline-flex items-center gap-2 rounded-full border border-[#D98A9E] bg-white/90 px-6 py-3.5 text-sm font-bold text-[#5E3758] shadow-2xs transition hover:border-[#9A7020] hover:bg-[#F6EEF0]"
              >
                Make It Custom
              </Link>
              <Link
                to="/our-story"
                className="inline-flex items-center gap-1.5 px-3 py-2 text-xs font-bold tracking-wider text-[#9A7020] uppercase transition hover:text-[#5E3758] hover:underline"
              >
                Our Story →
              </Link>
            </div>

            <div className="mt-8 grid grid-cols-3 gap-4 border-t border-[#EADCE0] pt-6 text-xs text-[#4A3E47]">
              <div>
                <span className="font-serif-display block text-lg font-bold text-[#161118]">
                  100% Custom-Made
                </span>
                Personalized with your names, photos &amp; wedding flowers
              </div>
              <div>
                <span className="font-serif-display tabular-price block text-lg font-bold text-[#161118]">
                  Instant Quotes
                </span>
                Live price estimates as you customize
              </div>
              <div>
                <span className="font-serif-display block text-lg font-bold text-[#161118]">
                  Chat on WhatsApp
                </span>
                Plan your design directly with Dhara
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4 lg:col-span-5">
            <div className="space-y-4">
              <div className="overflow-hidden rounded-t-full rounded-b-3xl border-2 border-[#C99B3B]/40 bg-[#F6EEF0] p-1 shadow-luxe">
                <img
                  src="/uploads/seed/grey-marble-ganesha-name-plate.jpg"
                  alt="Grey marble resin nameplate with golden lettering, Ganesha motif and lush greenery"
                  className="aspect-square w-full rounded-t-full rounded-b-2xl object-cover"
                />
              </div>
              <div className="overflow-hidden rounded-3xl border border-[#EADCE0] bg-white p-1 shadow-luxe">
                <img
                  src="/uploads/seed/engagement-ring-platter-hoop.jpg"
                  alt="Custom floral hoop and pearl resin engagement ring platter"
                  className="aspect-4/3 w-full rounded-2xl object-cover"
                />
              </div>
            </div>
            <div className="mt-6 space-y-4">
              <div className="overflow-hidden rounded-3xl border border-[#EADCE0] bg-white p-1 shadow-luxe">
                <img
                  src="/uploads/seed/krishna-jhula-royal-blue-1.jpg"
                  alt="Handmade royal blue and peacock feather resin Krishna Jhula swing"
                  className="aspect-4/3 w-full rounded-2xl object-cover"
                />
              </div>
              <div className="overflow-hidden rounded-t-3xl rounded-b-full border-2 border-[#D98A9E]/50 bg-[#F6EEF0] p-1 shadow-luxe">
                <img
                  src="/uploads/seed/ocean-magic-shell-name-plate.jpg"
                  alt="Ocean magic sea shell and golden flute round resin nameplate"
                  className="aspect-square w-full rounded-t-2xl rounded-b-full object-cover"
                />
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Categories */}
      {categories && categories.length > 0 && (
        <section className="mx-auto max-w-7xl px-4 py-14 sm:px-6 lg:px-8">
          <div className="flex items-end justify-between">
            <div>
              <span className="text-xs font-bold uppercase tracking-[0.16em] text-[#9A7020]">
                Browse by Category
              </span>
              <h2 className="mt-1 text-3xl font-bold text-[#161118] sm:text-4xl">
                What We Make
              </h2>
            </div>
            <Link
              to="/products"
              className="text-xs font-bold text-[#5E3758] hover:text-[#9A7020] hover:underline"
            >
              See all →
            </Link>
          </div>

          <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            {categories.map((c) => (
              <Link
                key={c.id}
                to={`/products?category=${encodeURIComponent(c.slug)}`}
                className="group relative overflow-hidden rounded-2xl border border-[#EADCE0] bg-white p-4 shadow-luxe transition duration-300 hover:-translate-y-0.5 hover:border-[#C99B3B]"
              >
                {c.imageUrl && (
                  <div className="aspect-4/3 overflow-hidden rounded-xl bg-[#F6EEF0]">
                    <img
                      src={c.imageUrl}
                      alt={c.name}
                      loading="lazy"
                      className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
                    />
                  </div>
                )}
                <h3 className="mt-3 text-base font-bold text-[#161118] group-hover:text-[#5E3758]">
                  {c.name}
                </h3>
                <p className="mt-1 line-clamp-2 text-xs leading-relaxed text-[#6E5A68]">
                  {c.description}
                </p>
              </Link>
            ))}
          </div>
        </section>
      )}

      <GoldScrollDivider className="my-2" />

      {/* Featured products */}
      <section className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <span className="text-xs font-bold uppercase tracking-[0.16em] text-[#9A7020]">
              Customer Favorites · Handcrafted Keepsakes
            </span>
            <h2 className="mt-1 text-3xl font-bold text-[#161118] sm:text-4xl">
              Popular Handmade Pieces
            </h2>
          </div>
          <Link
            to="/products"
            className="inline-flex items-center gap-1 text-xs font-bold text-[#5E3758] hover:text-[#9A7020] hover:underline"
          >
            View all designs <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>

        <div className="mt-6 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {featured?.items.slice(0, 6).map((p) => (
            <ProductCard key={p.id} product={p} />
          ))}
        </div>
      </section>

      {/* Value banner */}
      <section className="mx-auto my-14 max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="bg-obsidian-luxe relative overflow-hidden rounded-3xl border border-[#C99B3B]/40 px-6 py-10 text-[#FAF7F4] shadow-luxe sm:px-12">
          <div className="grid items-center gap-8 lg:grid-cols-12">
            <div className="lg:col-span-8">
              <span className="text-xs font-bold uppercase tracking-[0.16em] text-[#F3E5AB]">
                Custom Orders · Wedding Flowers · Housewarming · Gifting
              </span>
              <h2 className="mt-2 text-3xl font-bold text-white sm:text-4xl">
                Want to preserve your wedding flowers or design a custom nameplate?
              </h2>
              <p className="mt-2 max-w-2xl text-sm leading-relaxed text-[#E5D7DF]">
                Pick your size and style to see an instant price estimate. Then message Dhara on
                WhatsApp to choose your colors, share photos, and confirm delivery dates.
              </p>
            </div>
            <div className="flex flex-wrap gap-3 lg:col-span-4 lg:justify-end">
              <Link
                to="/custom"
                className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-[#C99B3B] via-[#E5C158] to-[#C99B3B] px-6 py-3 text-xs font-bold text-[#161118] shadow-sm transition hover:brightness-105"
              >
                <HeartHandshake className="h-4 w-4" /> Start a Custom Order
              </Link>
              {bootstrap?.business.whatsAppUrl && (
                <a
                  href={bootstrap.business.whatsAppUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-2 rounded-full border border-[#D98A9E]/40 bg-[#5E3758]/40 px-5 py-3 text-xs font-bold text-white transition hover:border-[#F3E5AB]"
                >
                  <MessageCircle className="h-4 w-4 text-[#4ADE80]" /> Chat on WhatsApp
                </a>
              )}
            </div>
          </div>
          <div className="mt-8 grid gap-4 border-t border-[#C99B3B]/25 pt-6 text-xs text-[#D5C5CE] sm:grid-cols-3">
            <div className="flex items-center gap-2">
              <Globe2 className="h-4 w-4 shrink-0 text-[#F3E5AB]" />
              Handmade in Nadiad, Gujarat · Shipping across India &amp; worldwide
            </div>
            <div>Safely packed in foam-lined boxes for worry-free worldwide shipping</div>
            <div>We send you preview photos on WhatsApp before pouring the final glossy layer</div>
          </div>
        </div>
      </section>

      {/* Studio Numbers & Our Story Teaser */}
      <section className="mx-auto mb-10 max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe sm:p-10">
          <div className="grid items-center gap-8 lg:grid-cols-12">
            <div className="lg:col-span-5">
              <span className="text-xs font-bold tracking-[0.22em] text-[#9A7020] uppercase">
                ✦ &nbsp; Behind The Craft
              </span>
              <h2 className="font-serif-display mt-2 text-2xl font-bold text-[#161118] sm:text-3xl">
                Each pour with love — no two pieces are ever the same
              </h2>
              <p className="mt-2 text-xs leading-relaxed text-[#4A3E47] sm:text-sm">
                From humble pooja thalis and keepsakes in 2022 to custom wedding varmala clocks and
                house nameplates shipped worldwide — discover how we craft every piece by hand in
                Gujarat.
              </p>
              <div className="mt-5 flex flex-wrap gap-3">
                <Link
                  to="/our-story"
                  className="inline-flex items-center gap-1.5 text-xs font-bold tracking-wider text-[#5E3758] uppercase hover:text-[#9A7020] hover:underline"
                >
                  Read Our Story <ArrowRight className="h-3.5 w-3.5" />
                </Link>
                <span className="text-[#EADCE0]">·</span>
                <Link
                  to="/our-story#process"
                  className="inline-flex items-center gap-1.5 text-xs font-bold tracking-wider text-[#9A7020] uppercase hover:text-[#5E3758] hover:underline"
                >
                  How Flower Preservation Works →
                </Link>
              </div>
            </div>

            <div className="grid grid-cols-2 divide-y divide-[#EADCE0] border-t border-[#EADCE0] pt-6 sm:grid-cols-4 sm:divide-x sm:divide-y-0 lg:col-span-7 lg:border-t-0 lg:border-l lg:pt-0 lg:pl-8">
              <div className="px-3 py-3 text-center">
                <p className="font-serif-display text-3xl font-bold text-[#9A7020] sm:text-4xl">
                  2022
                </p>
                <p className="mt-1 text-[10px] font-bold tracking-[0.2em] text-[#6E5A68] uppercase">
                  Since
                </p>
              </div>
              <div className="px-3 py-3 text-center">
                <p className="font-serif-display text-3xl font-bold text-[#9A7020] sm:text-4xl">
                  1000+
                </p>
                <p className="mt-1 text-[10px] font-bold tracking-[0.2em] text-[#6E5A68] uppercase">
                  Pieces Made
                </p>
              </div>
              <div className="px-3 py-3 text-center">
                <p className="font-serif-display text-3xl font-bold text-[#9A7020] sm:text-4xl">
                  500+
                </p>
                <p className="mt-1 text-[10px] font-bold tracking-[0.2em] text-[#6E5A68] uppercase">
                  Happy Families
                </p>
              </div>
              <div className="px-3 py-3 text-center">
                <p className="font-serif-display text-3xl font-bold text-[#9A7020] sm:text-4xl">
                  20+
                </p>
                <p className="mt-1 text-[10px] font-bold tracking-[0.2em] text-[#6E5A68] uppercase">
                  Cities &amp; Global
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};
