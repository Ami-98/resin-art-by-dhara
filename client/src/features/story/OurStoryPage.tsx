import React, { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import {
  ArrowRight,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Globe2,
  HeartHandshake,
  Instagram,
  MessageCircle,
  PackageCheck,
  ShieldCheck,
  Sparkles,
  Sun,
  Truck,
} from 'lucide-react';
import { GoldScrollDivider, RoseWatermarkBadge } from '../../components/BrandLogo';
import { useRegion } from '../region/RegionContext';

const GALLERY_SLIDES = [
  {
    src: '/uploads/seed/varmala-preservation-couple-clock.jpg',
    title: 'Wedding Varmala & Couple Photo Wall Clock',
    caption: 'Real wedding garland petals dried, colour-preserved, and cast in crystal-clear resin with golden Roman numerals.',
  },
  {
    src: '/uploads/seed/grey-marble-ganesha-name-plate.jpg',
    title: 'Grey Marble & Lush Greenery Ganesha Nameplate',
    caption: 'Hand-poured marble veining paired with 3D metallic gold lettering and auspicious Ganeshji motif.',
  },
  {
    src: '/uploads/seed/krishna-jhula-royal-blue-1.jpg',
    title: 'Royal Blue & Peacock Feather Laddu Gopal Jhula',
    caption: 'Handcrafted devotional swing for Balkrishna adorned with Morpankh detailing and golden arch work.',
  },
  {
    src: '/uploads/seed/engagement-ring-platter-hoop.jpg',
    title: 'Floral Hoop & Pearl Engagement Ring Platter',
    caption: 'Custom ring ceremony platter personalized with couple initials, dates, and pastel florals.',
  },
  {
    src: '/uploads/seed/gujarati-kankotri-wedding-frame.jpg',
    title: 'Gujarati Kankotri (Wedding Invite) Keepsake Frame',
    caption: 'Preserve your family wedding invitation forever alongside real flowers and golden foil.',
  },
  {
    src: '/uploads/seed/ocean-magic-shell-name-plate.jpg',
    title: 'Ocean Magic Sea-Shell & Golden Flute Nameplate',
    caption: 'Multi-layered ocean wave pour with real sea shells, pearl pigments, and Bansuri motif.',
  },
];

const PROCESS_STEPS = [
  {
    step: '01',
    title: 'Choose Your Design & Instant Quote',
    description:
      'Browse our collection of house nameplates, varmala wall clocks, ring platters, and Krishna jhulas. Select your size and custom options to see a live price estimate right on the page.',
  },
  {
    step: '02',
    title: 'Plan Directly on WhatsApp & Send Flowers',
    description:
      'Connect with Dhara on WhatsApp to finalize colours, fonts, and Hindi/Gujarati/English lettering. If preserving wedding flowers or kankotri invites, we guide you on simple courier packing to our Nadiad studio.',
  },
  {
    step: '03',
    title: 'Layout Preview Before the Final Pour',
    description:
      'We carefully silica-dry your flowers, pour the base layer, and arrange your photos, names, and petals. Before sealing the final glossy top coat, we send you preview photos on WhatsApp for your approval.',
  },
  {
    step: '04',
    title: 'Multi-Stage Curing & Foam-Lined Shipping',
    description:
      'Every piece cures slowly for a glass-like, bubble-free finish with hand-gilded edges, then ships safely in custom foam-lined boxes across Gujarat, India, USA, Canada, and worldwide.',
  },
];

const FAQS = [
  {
    q: 'How do I send my wedding varmala (garland) or flowers for preservation?',
    a: 'Even if your wedding was a few days ago—or you have already dried your flowers at home—you can courier them to our studio in Nadiad, Gujarat. Keep the petals in a breathable paper bag or cardboard box (avoid airtight plastic bags so moisture does not trap), and message us on WhatsApp for the exact studio courier address and packing tips.',
  },
  {
    q: 'Will I get to see how my nameplate or clock looks before it is shipped?',
    a: 'Yes! For every custom order, we share a layout preview photo or video on WhatsApp showing your lettering placement, photo arrangement, and flower composition before pouring the final sealing coat.',
  },
  {
    q: 'How long does a custom resin order take to make?',
    a: 'Standard custom nameplates and ring platters take 7–10 days for multi-layer pouring and full curing. Wedding flower (varmala) preservation takes 14–21 days because fresh petals must first be professionally dehydrated so they retain their natural colour inside the resin.',
  },
  {
    q: 'How should I care for and clean my resin art piece?',
    a: 'Wipe gently with a soft microfiber cloth and mild water or glass cleaner. Avoid harsh scrubbing pads, alcohol/acetone cleaners, and prolonged direct outdoor midday sunlight to keep the resin crystal-clear for years.',
  },
  {
    q: 'Do you ship outside Gujarat and internationally (USA / Canada)?',
    a: 'Yes! We ship across all Indian states via insured surface and air couriers, and we regularly ship custom house nameplates, kankotri frames, and varmala keepsakes to families in the USA, Canada, UK, and Australia in double-walled, foam-lined packaging.',
  },
];

export const OurStoryPage: React.FC = () => {
  const { bootstrap, activeWhatsAppUrl } = useRegion();
  const location = useLocation();
  const [slideIdx, setSlideIdx] = useState(0);
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  const bizName = bootstrap?.business.name ?? 'Resin Delights by Dhara';
  const instagramUrl =
    bootstrap?.business.social?.instagram || 'https://www.instagram.com/resin_delights_by_dhara/';
  const instagramHandle =
    instagramUrl
      .replace(/\/+$/, '')
      .split('/')
      .pop()
      ?.replace(/^@/, '') || 'resin_delights_by_dhara';
  const whatsAppUrl = activeWhatsAppUrl;

  useEffect(() => {
    document.title = `Our Story & Craft | ${bizName}`;
  }, [bizName]);

  useEffect(() => {
    if (location.hash) {
      const id = location.hash.replace('#', '');
      const el = document.getElementById(id);
      if (el) {
        setTimeout(() => el.scrollIntoView({ behavior: 'smooth', block: 'start' }), 100);
      }
    } else {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }, [location.hash]);

  const prevSlide = () =>
    setSlideIdx((prev) => (prev === 0 ? GALLERY_SLIDES.length - 1 : prev - 1));
  const nextSlide = () =>
    setSlideIdx((prev) => (prev === GALLERY_SLIDES.length - 1 ? 0 : prev + 1));

  const currentSlide = GALLERY_SLIDES[slideIdx];

  return (
    <div className="bg-[#FAF7F4] text-[#161118]">
      {/* 1. Editorial Luxury Hero Section (Inspired by dharaartcreation.com/our-story) */}
      <section className="bg-obsidian-luxe relative flex min-h-[68vh] items-center justify-center overflow-hidden border-b border-[#C99B3B]/30 px-4 py-20 text-center">
        {/* Subtle giant watermark year in background */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 flex items-center justify-center select-none"
        >
          <span
            className="font-serif-display text-[24vw] leading-none font-bold tracking-tight text-transparent"
            style={{ WebkitTextStroke: '1px rgba(201, 155, 59, 0.09)' }}
          >
            2022
          </span>
        </div>

        {/* Soft radial gold glow */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute top-1/2 left-1/2 h-[55vw] w-[55vw] -translate-x-1/2 -translate-y-1/2 rounded-full"
          style={{
            background: 'radial-gradient(circle, rgba(201, 155, 59, 0.09) 0%, transparent 65%)',
          }}
        />

        <div className="relative z-10 mx-auto max-w-3xl px-4">
          <p className="text-[11px] font-bold tracking-[0.38em] text-[#F3E5AB] uppercase">
            ✦ &nbsp; {bizName} · Nadiad &amp; Uttarsanda, Gujarat &nbsp; ✦
          </p>
          <h1 className="font-serif-display mt-5 text-5xl leading-[0.98] font-bold text-[#FAF7F4] sm:text-6xl lg:text-7xl">
            Our <span className="italic font-normal text-[#F3E5AB]">Story</span>
          </h1>

          {/* Gold Diamond Divider */}
          <div className="my-6 flex items-center justify-center gap-4">
            <div className="h-px w-16 bg-gradient-to-r from-transparent to-[#C99B3B]" />
            <span className="text-xs text-[#C99B3B]">✦</span>
            <div className="h-px w-16 bg-gradient-to-l from-transparent to-[#C99B3B]" />
          </div>

          <p className="mx-auto max-w-xl text-base leading-relaxed text-[#E5D7DF] sm:text-lg">
            Handmade with love. Each pour, a new story — turning wedding flowers, auspicious
            blessings, and family milestones into timeless resin keepsakes.
          </p>

          <div className="mt-8 flex justify-center">
            <RoseWatermarkBadge light />
          </div>
        </div>
      </section>

      {/* 2. Where It All Began — Two-Column Editorial Section with Gold Corner Frame */}
      <section className="mx-auto max-w-7xl px-4 py-16 sm:px-6 lg:px-8 lg:py-24">
        <div className="grid items-center gap-12 lg:grid-cols-12 lg:gap-16">
          <div className="lg:col-span-6">
            <p className="text-xs font-bold tracking-[0.3em] text-[#9A7020] uppercase">
              ✦ &nbsp; Where It All Began
            </p>
            <h2 className="font-serif-display mt-3 text-3xl leading-tight font-bold text-[#161118] sm:text-4xl lg:text-[2.65rem]">
              It started with a love for{' '}
              <span className="italic text-[#5E3758]">handcrafted keepsakes &amp; pooja art</span>
            </h2>
            <div className="mt-6 space-y-4 text-sm leading-relaxed text-[#4A3E47] sm:text-base">
              <p>
                What began quietly in Gujarat — pouring small custom gifts, festive pooja thalis,
                and personalized keepsakes one order at a time — soon blossomed into{' '}
                <strong className="text-[#161118]">{bizName}</strong>. There was no factory line,
                just an obsession with colour, natural florals, golden leafing, and the luminous
                depth of crystal-clear resin.
              </p>
              <p>
                Every pour taught us something new: how real wedding varmala petals hold their
                velvet tones when dried with care, how marble veins flow across a house entrance
                nameplate, and how a miniature Laddu Gopal jhula can become the heart of a family
                mandir.
              </p>
              <p>
                Today, from our studio in <strong>Nadiad &amp; Uttarsanda, Gujarat</strong>, Dhara
                Shah handcrafts bespoke pieces for families across India, the USA, Canada, and
                beyond — while keeping the exact same personal touch and WhatsApp collaboration as
                our very first piece.
              </p>
            </div>

            <div className="mt-8 flex flex-wrap items-center gap-4">
              <Link
                to="/products"
                className="inline-flex items-center gap-2 rounded-full border border-[#C99B3B]/60 bg-obsidian-luxe px-6 py-3 text-xs font-bold tracking-wider text-[#FAF7F4] uppercase shadow-luxe transition hover:border-[#F3E5AB] hover:text-[#F3E5AB]"
              >
                Explore Collection <ArrowRight className="h-4 w-4 text-[#C99B3B]" />
              </Link>
              <a
                href={instagramUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-2 rounded-full border border-[#D98A9E] bg-white px-5 py-3 text-xs font-bold text-[#5E3758] transition hover:border-[#9A7020]"
              >
                <Instagram className="h-4 w-4 text-[#C86D8A]" /> @{instagramHandle}
              </a>
            </div>
          </div>

          <div className="relative lg:col-span-6">
            <div className="relative mx-auto max-w-md overflow-hidden rounded-3xl border border-[#EADCE0] bg-white p-2 shadow-luxe">
              <img
                src="/uploads/seed/varmala-preservation-couple-clock.jpg"
                alt="Handmade wedding varmala preservation resin clock by Dhara"
                className="aspect-4/5 w-full rounded-2xl object-cover"
              />
              {/* Decorative Gold Corner Brackets */}
              <div className="pointer-events-none absolute top-5 right-5 h-14 w-14 border-t-2 border-r-2 border-[#C99B3B]/80" />
              <div className="pointer-events-none absolute bottom-5 left-5 h-14 w-14 border-b-2 border-l-2 border-[#C99B3B]/80" />
            </div>
            <div
              aria-hidden="true"
              className="pointer-events-none absolute -right-3 -bottom-3 -z-10 h-1/2 w-1/2 rounded-3xl border border-[#C99B3B]/35"
            />
          </div>
        </div>
      </section>

      {/* 3. Studio Philosophy & Quote Block */}
      <section className="border-y border-[#EADCE0] bg-[#F6EEF0]/70 py-16">
        <div className="mx-auto max-w-4xl px-4 text-center sm:px-6">
          <p className="text-xs font-bold tracking-[0.3em] text-[#9A7020] uppercase">
            ✦ &nbsp; What We Create
          </p>
          <h2 className="font-serif-display mt-2 text-3xl font-bold text-[#161118] sm:text-4xl">
            Unique pieces, poured with intention
          </h2>
          <blockquote className="font-serif-display my-8 border-y border-[#D98A9E]/50 py-6 text-2xl italic text-[#5E3758] sm:text-3xl">
            &ldquo;Each pour with love — no two pieces are ever the same.&rdquo;
          </blockquote>
          <p className="mx-auto max-w-2xl text-sm leading-relaxed text-[#4A3E47] sm:text-base">
            Because resin is fluid and natural flowers carry their own organic shapes, every
            nameplate, clock, and ring platter we pour has one-of-a-kind swirls, gold leaf
            shimmer, and depth made exclusively for your home.
          </p>
        </div>
      </section>

      {/* 4. 4-Column Studio Milestone Stats Grid */}
      <section className="border-b border-[#EADCE0] bg-white py-12">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="grid grid-cols-2 divide-y divide-[#EADCE0] sm:grid-cols-4 sm:divide-x sm:divide-y-0">
            <div className="px-4 py-5 text-center">
              <p className="font-serif-display text-4xl font-bold text-[#9A7020] sm:text-5xl">
                2022
              </p>
              <p className="mt-1.5 text-[11px] font-bold tracking-[0.22em] text-[#6E5A68] uppercase">
                Crafting Since
              </p>
            </div>
            <div className="px-4 py-5 text-center">
              <p className="font-serif-display text-4xl font-bold text-[#9A7020] sm:text-5xl">
                1000+
              </p>
              <p className="mt-1.5 text-[11px] font-bold tracking-[0.22em] text-[#6E5A68] uppercase">
                Pieces Handcrafted
              </p>
            </div>
            <div className="px-4 py-5 text-center">
              <p className="font-serif-display text-4xl font-bold text-[#9A7020] sm:text-5xl">
                500+
              </p>
              <p className="mt-1.5 text-[11px] font-bold tracking-[0.22em] text-[#6E5A68] uppercase">
                Happy Families
              </p>
            </div>
            <div className="px-4 py-5 text-center">
              <p className="font-serif-display text-4xl font-bold text-[#9A7020] sm:text-5xl">
                20+
              </p>
              <p className="mt-1.5 text-[11px] font-bold tracking-[0.22em] text-[#6E5A68] uppercase">
                Cities &amp; Worldwide
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* 5. How It's Made — 4-Step Custom & Flower Preservation Process (#process) */}
      <section id="process" className="mx-auto max-w-7xl scroll-mt-24 px-4 py-16 sm:px-6 lg:px-8">
        <div className="text-center">
          <p className="text-xs font-bold tracking-[0.3em] text-[#9A7020] uppercase">
            ✦ &nbsp; How It&rsquo;s Made
          </p>
          <h2 className="font-serif-display mt-2 text-3xl font-bold text-[#161118] sm:text-4xl">
            From Your Special Moment to Forever in Resin
          </h2>
          <p className="mx-auto mt-2 max-w-2xl text-sm text-[#4A3E47]">
            Whether you are ordering a house entrance nameplate or preserving your wedding varmala,
            here is how we craft your piece step by step.
          </p>
        </div>

        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {PROCESS_STEPS.map((item) => (
            <div
              key={item.step}
              className="relative flex flex-col justify-between rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe"
            >
              <div>
                <span className="font-serif-display inline-flex h-10 w-10 items-center justify-center rounded-full border border-[#C99B3B]/50 bg-[#F6EEF0] text-sm font-bold text-[#5E3758]">
                  {item.step}
                </span>
                <h3 className="mt-4 text-lg font-bold text-[#161118]">{item.title}</h3>
                <p className="mt-2 text-xs leading-relaxed text-[#4A3E47]">{item.description}</p>
              </div>
              <div className="mt-4 border-t border-[#EADCE0]/70 pt-3 text-[11px] font-semibold text-[#9A7020]">
                Handcrafted in Nadiad Studio
              </div>
            </div>
          ))}
        </div>
      </section>

      <GoldScrollDivider className="my-2" />

      {/* 6. Behind The Craft — Interactive Showcase Carousel */}
      <section className="mx-auto max-w-6xl px-4 py-14 sm:px-6 lg:px-8">
        <div className="mb-8 text-center">
          <p className="text-xs font-bold tracking-[0.3em] text-[#9A7020] uppercase">
            ✦ &nbsp; Behind The Craft
          </p>
          <h2 className="font-serif-display mt-2 text-3xl font-bold text-[#161118] sm:text-4xl">
            Glimpses of Our Studio Work
          </h2>
        </div>

        <div className="relative overflow-hidden rounded-3xl border border-[#C99B3B]/40 bg-obsidian-luxe shadow-luxe">
          <div className="grid items-center lg:grid-cols-12">
            <div className="relative aspect-4/3 w-full overflow-hidden bg-[#161118] lg:col-span-7">
              <img
                src={currentSlide.src}
                alt={currentSlide.title}
                className="h-full w-full object-cover transition duration-500"
              />
              <span className="absolute top-4 right-4 rounded-full border border-[#C99B3B]/40 bg-[#161118]/80 px-3 py-1 text-[11px] font-semibold tracking-widest text-[#F3E5AB]">
                {slideIdx + 1} / {GALLERY_SLIDES.length}
              </span>
            </div>

            <div className="flex flex-col justify-between p-6 text-[#FAF7F4] sm:p-10 lg:col-span-5">
              <div>
                <span className="text-[11px] font-bold tracking-[0.24em] text-[#F3E5AB] uppercase">
                  Signature Handcrafted Piece
                </span>
                <h3 className="font-serif-display mt-2 text-2xl font-bold text-white sm:text-3xl">
                  {currentSlide.title}
                </h3>
                <p className="mt-3 text-sm leading-relaxed text-[#E5D7DF]">
                  {currentSlide.caption}
                </p>
              </div>

              <div className="mt-8 flex items-center justify-between border-t border-[#C99B3B]/25 pt-5">
                <div className="flex items-center gap-2">
                  {GALLERY_SLIDES.map((_, idx) => (
                    <button
                      key={idx}
                      type="button"
                      aria-label={`View slide ${idx + 1}`}
                      onClick={() => setSlideIdx(idx)}
                      className={`h-2 rounded-full transition-all ${
                        idx === slideIdx ? 'w-7 bg-[#C99B3B]' : 'w-2 bg-white/30 hover:bg-white/50'
                      }`}
                    />
                  ))}
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={prevSlide}
                    aria-label="Previous piece"
                    className="inline-flex h-9 w-9 items-center justify-center rounded-full border border-[#C99B3B]/40 bg-white/5 text-[#F3E5AB] transition hover:border-[#F3E5AB] hover:bg-white/15"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={nextSlide}
                    aria-label="Next piece"
                    className="inline-flex h-9 w-9 items-center justify-center rounded-full border border-[#C99B3B]/40 bg-white/5 text-[#F3E5AB] transition hover:border-[#F3E5AB] hover:bg-white/15"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* 7. Shipping, Packaging & Care Info (#shipping) */}
      <section
        id="shipping"
        className="mx-auto max-w-7xl scroll-mt-24 px-4 py-12 sm:px-6 lg:px-8"
      >
        <div className="grid gap-6 md:grid-cols-3">
          <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe">
            <Truck className="h-7 w-7 text-[#9A7020]" />
            <h3 className="mt-3 text-lg font-bold text-[#161118]">
              Safe Worldwide &amp; Pan-India Shipping
            </h3>
            <p className="mt-2 text-xs leading-relaxed text-[#4A3E47]">
              Every nameplate, clock, and platter is wrapped in protective bubble layers and
              packed inside sturdy foam-lined boxes. We ship across Gujarat, all of India, USA,
              and Canada with tracking shared on WhatsApp.
            </p>
          </div>

          <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe">
            <Sun className="h-7 w-7 text-[#9A7020]" />
            <h3 className="mt-3 text-lg font-bold text-[#161118]">
              Resin Care &amp; Placement Guide
            </h3>
            <p className="mt-2 text-xs leading-relaxed text-[#4A3E47]">
              Install house nameplates in shaded entrances or lobbies away from harsh direct
              midday sun and rain. Clean gently with a soft damp microfiber cloth—never use
              alcohol, thinner, or abrasive scrubbers.
            </p>
          </div>

          <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe">
            <PackageCheck className="h-7 w-7 text-[#9A7020]" />
            <h3 className="mt-3 text-lg font-bold text-[#161118]">
              Damage Protection Promise
            </h3>
            <p className="mt-2 text-xs leading-relaxed text-[#4A3E47]">
              Because each piece is custom-made with your names or flowers, we verify your layout
              before the final pour. In the rare event of transit damage, share an unboxing video
              within 24 hours of delivery so we can assist immediately.
            </p>
          </div>
        </div>
      </section>

      {/* 8. Frequently Asked Questions (#faq) */}
      <section id="faq" className="mx-auto max-w-4xl scroll-mt-24 px-4 py-14 sm:px-6 lg:px-8">
        <div className="text-center">
          <p className="text-xs font-bold tracking-[0.3em] text-[#9A7020] uppercase">
            ✦ &nbsp; Got Questions?
          </p>
          <h2 className="font-serif-display mt-2 text-3xl font-bold text-[#161118] sm:text-4xl">
            Frequently Asked Questions
          </h2>
        </div>

        <div className="mt-8 space-y-3">
          {FAQS.map((item, idx) => {
            const isOpen = openFaq === idx;
            return (
              <div
                key={idx}
                className="overflow-hidden rounded-2xl border border-[#EADCE0] bg-white shadow-2xs"
              >
                <button
                  type="button"
                  onClick={() => setOpenFaq(isOpen ? null : idx)}
                  className="flex w-full items-center justify-between gap-4 px-6 py-4 text-left text-sm font-bold text-[#161118] transition hover:bg-[#F6EEF0]/50 sm:text-base"
                >
                  <span>{item.q}</span>
                  <ChevronDown
                    className={`h-4 w-4 shrink-0 text-[#9A7020] transition-transform duration-200 ${
                      isOpen ? 'rotate-180' : ''
                    }`}
                  />
                </button>
                {isOpen && (
                  <div className="border-t border-[#EADCE0]/60 px-6 py-4 text-xs leading-relaxed text-[#4A3E47] sm:text-sm">
                    {item.a}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </section>

      {/* 9. Become Part Of The Story — Bottom CTA */}
      <section className="mx-auto max-w-7xl px-4 pb-16 sm:px-6 lg:px-8">
        <div className="bg-obsidian-luxe relative overflow-hidden rounded-3xl border border-[#C99B3B]/40 px-6 py-12 text-center text-[#FAF7F4] shadow-luxe sm:px-12">
          <p className="text-xs font-bold tracking-[0.32em] text-[#F3E5AB] uppercase">
            ✦ &nbsp; Become Part Of Our Story
          </p>
          <h2 className="font-serif-display mt-3 text-3xl font-bold text-white sm:text-4xl">
            Every piece carries a little piece of our heart
          </h2>
          <p className="mx-auto mt-2 max-w-xl text-sm text-[#E5D7DF]">
            Browse our handmade collection or message Dhara on WhatsApp to design something made
            just for you.
          </p>
          <div className="mt-7 flex flex-wrap justify-center gap-3.5">
            <Link
              to="/products"
              className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-[#C99B3B] via-[#E5C158] to-[#C99B3B] px-7 py-3.5 text-xs font-bold tracking-wider text-[#161118] uppercase shadow-sm transition hover:brightness-105"
            >
              <Sparkles className="h-4 w-4" /> Shop Our Collection
            </Link>
            <Link
              to="/custom"
              className="inline-flex items-center gap-2 rounded-full border border-[#C99B3B]/60 bg-white/5 px-7 py-3.5 text-xs font-bold tracking-wider text-[#F3E5AB] uppercase transition hover:border-[#F3E5AB] hover:bg-white/10"
            >
              <HeartHandshake className="h-4 w-4" /> Order a Custom Piece
            </Link>
            <a
              href={whatsAppUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-2 rounded-full border border-[#D98A9E]/40 bg-[#146348] px-6 py-3.5 text-xs font-bold tracking-wider text-white uppercase transition hover:bg-[#0F4F39]"
            >
              <MessageCircle className="h-4 w-4 text-[#F3E5AB]" /> Chat on WhatsApp
            </a>
          </div>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-6 border-t border-[#C99B3B]/20 pt-5 text-xs text-[#D5C5CE]">
            <span className="inline-flex items-center gap-1.5">
              <Globe2 className="h-3.5 w-3.5 text-[#F3E5AB]" /> Nadiad &amp; Uttarsanda, Gujarat
            </span>
            <span className="inline-flex items-center gap-1.5">
              <ShieldCheck className="h-3.5 w-3.5 text-[#F3E5AB]" /> WhatsApp Preview Before Final Pour
            </span>
          </div>
        </div>
      </section>
    </div>
  );
};
