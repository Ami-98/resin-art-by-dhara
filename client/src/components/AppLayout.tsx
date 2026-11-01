import React from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { Instagram, Mail, MapPin, MessageCircle, Phone, Search, Sparkles } from 'lucide-react';
import { useRegion } from '../features/region/RegionContext';
import { BrandLogoMark, RoseWatermarkBadge } from './BrandLogo';

export const AppLayout: React.FC = () => {
  const { bootstrap, activeWhatsAppUrl } = useRegion();
  const bizName = bootstrap?.business.name ?? 'Resin Delights by Dhara';
  const tagline = bootstrap?.business.tagline ?? 'Nadiad, Gujarat · Preserving Memories in Resin';
  const studioLocation = bootstrap?.business.location ?? 'Nadiad & Uttarsanda, Gujarat, India';
  const contactEmail = bootstrap?.business.contactEmail || 'resinartbydhara@gmail.com';
  const whatsAppUrl = activeWhatsAppUrl;
  const instagramUrl =
    bootstrap?.business.social?.instagram || 'https://www.instagram.com/resin_delights_by_dhara/';
  const instagramHandle =
    instagramUrl
      .replace(/\/+$/, '')
      .split('/')
      .pop()
      ?.replace(/^@/, '') || 'resin_delights_by_dhara';

  const navClass = ({ isActive }: { isActive: boolean }) =>
    `relative text-sm font-medium transition py-1 whitespace-nowrap ${
      isActive
        ? 'text-[#5E3758] font-semibold after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:rounded-full after:bg-gradient-to-r after:from-[#C86D8A] after:to-[#C99B3B]'
        : 'text-[#4A3E47] hover:text-[#5E3758]'
    }`;

  return (
    <div className="flex min-h-screen flex-col bg-[#FAF7F4] text-[#161118]">
      {/* Top Luxury Announcement / Craft Strip */}
      <div className="bg-obsidian-luxe border-b border-[#C99B3B]/30 px-3 py-1.5 text-center text-[11px] font-medium tracking-[0.14em] text-[#F3E5AB] uppercase">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-center gap-x-4 gap-y-1">
          <span className="inline-flex items-center gap-1.5">
            <Sparkles className="h-3 w-3 text-[#C99B3B]" /> Handmade With Love in Gujarat
          </span>
          <span className="hidden text-[#C99B3B]/60 sm:inline">✦</span>
          <span className="hidden sm:inline">100% Custom Nameplates &amp; Varmala Preservation</span>
          <span className="hidden text-[#C99B3B]/60 md:inline">✦</span>
          <span className="hidden md:inline">Safe Shipping Across India, USA &amp; Canada</span>
        </div>
      </div>

      <header className="sticky top-0 z-30 border-b border-[#EADCE0] bg-[#FAF7F4]/90 backdrop-blur-md">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8">
          <Link to="/" className="group flex items-center gap-3" aria-label={bizName}>
            <div className="relative flex items-center justify-center rounded-full p-0.5 shadow-sm transition duration-300 group-hover:scale-105">
              <BrandLogoMark className="h-11 w-11" />
            </div>
            <div>
              <div className="flex items-baseline gap-1.5">
                <span className="font-serif-display text-lg font-bold tracking-tight text-[#161118] sm:text-xl">
                  Resin Delights
                </span>
                <span className="font-serif-display text-base font-semibold text-[#9A7020] italic sm:text-lg">
                  by Dhara
                </span>
              </div>
              <span className="block text-[10px] font-bold tracking-[0.14em] text-[#6E5A68] uppercase">
                {tagline}
              </span>
            </div>
          </Link>

          <nav className="hidden items-center gap-7 md:flex">
            <NavLink to="/" end className={navClass}>
              Home
            </NavLink>
            <NavLink to="/products" className={navClass}>
              All Designs
            </NavLink>
            <NavLink to="/custom" className={navClass}>
              Custom Orders
            </NavLink>
            <NavLink to="/our-story" className={navClass}>
              Our Story
            </NavLink>
            <NavLink to="/contact" className={navClass}>
              Contact
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            <Link
              to="/products"
              aria-label="Search designs"
              title="Search nameplates, clocks, jhulas…"
              className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-[#D98A9E]/60 bg-white text-[#5E3758] shadow-2xs transition hover:border-[#C99B3B] hover:text-[#9A7020]"
            >
              <Search className="h-4 w-4" />
            </Link>
            <a
              href={instagramUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label="Instagram"
              title={`@${instagramHandle} on Instagram`}
              className="inline-flex h-8 w-8 items-center justify-center rounded-full border border-[#D98A9E]/60 bg-white text-[#5E3758] shadow-2xs transition hover:border-[#C99B3B] hover:text-[#9A7020]"
            >
              <Instagram className="h-4 w-4" />
            </a>
            <a
              href={whatsAppUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1.5 rounded-full border border-[#C99B3B]/40 bg-[#146348] px-3 py-1.5 text-xs font-semibold text-white shadow-2xs transition hover:bg-[#0F4F39]"
            >
              <MessageCircle className="h-3.5 w-3.5 text-[#F3E5AB]" />
              <span className="hidden sm:inline">WhatsApp</span>
            </a>
          </div>
        </div>

        {/* Mobile navigation bar */}
        <nav className="flex items-center justify-around overflow-x-auto border-t border-[#EADCE0]/70 bg-[#FAF7F4] px-3 py-2 md:hidden">
          <NavLink to="/" end className={navClass}>
            Home
          </NavLink>
          <NavLink to="/products" className={navClass}>
            All Designs
          </NavLink>
          <NavLink to="/custom" className={navClass}>
            Custom Orders
          </NavLink>
          <NavLink to="/our-story" className={navClass}>
            Our Story
          </NavLink>
          <NavLink to="/contact" className={navClass}>
            Contact
          </NavLink>
        </nav>
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      {/* 4-Column Luxury Footer (Inspired by dharaartcreation.com/our-story) */}
      <footer className="mt-16 border-t border-[#C99B3B]/30 bg-[#110D13] text-[#F6EEF0]">
        <div className="mx-auto max-w-7xl px-4 pt-16 pb-8 sm:px-6 lg:px-8">
          <div className="mb-12 grid grid-cols-2 gap-8 md:grid-cols-4 lg:gap-10">
            {/* Column 1: Brand, Tagline, Description & Social Icon Buttons */}
            <div className="col-span-2 md:col-span-1">
              <Link to="/" className="mb-4 flex items-center gap-3">
                <img
                  src="/uploads/seed/dhara-logo.jpg"
                  alt={bizName}
                  className="h-11 w-11 shrink-0 rounded-full border-2 border-[#C99B3B] object-cover shadow-md"
                />
                <div>
                  <p className="font-serif-display text-lg font-semibold tracking-wide text-[#FAF7F4]">
                    {bizName}
                  </p>
                  <p className="text-[9px] font-bold tracking-[0.25em] text-[#C99B3B] uppercase">
                    Handmade With Love
                  </p>
                </div>
              </Link>

              <p className="mb-4 text-xs leading-relaxed text-[#B9A8B3]">
                Unique handcrafted resin art — gold leaf nameplates, wedding varmala preservation,
                Laddu Gopal jhulas, and memory keepsakes poured with love. Every piece tells a
                story.
              </p>

              <div className="mb-5">
                <RoseWatermarkBadge light />
              </div>

              {/* 3 Square Bordered Action Icons: Instagram, WhatsApp, Email */}
              <div className="flex items-center gap-2.5">
                <a
                  href={instagramUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label="Instagram"
                  title={`@${instagramHandle}`}
                  className="flex h-8 w-8 items-center justify-center rounded-sm border border-[#342838] text-[#B9A8B3] transition-all duration-200 hover:border-[#C99B3B] hover:text-[#F3E5AB]"
                >
                  <Instagram className="h-3.5 w-3.5" />
                </a>
                <a
                  href={whatsAppUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label="WhatsApp"
                  title="Chat on WhatsApp"
                  className="flex h-8 w-8 items-center justify-center rounded-sm border border-[#342838] text-[#B9A8B3] transition-all duration-200 hover:border-[#25D366] hover:text-[#25D366]"
                >
                  <Phone className="h-3.5 w-3.5" />
                </a>
                <a
                  href={`mailto:${contactEmail}`}
                  aria-label="Email"
                  title={contactEmail}
                  className="flex h-8 w-8 items-center justify-center rounded-sm border border-[#342838] text-[#B9A8B3] transition-all duration-200 hover:border-[#C99B3B] hover:text-[#F3E5AB]"
                >
                  <Mail className="h-3.5 w-3.5" />
                </a>
              </div>
            </div>

            {/* Column 2: SHOP */}
            <div>
              <h4 className="mb-4 text-[10px] font-bold tracking-[0.25em] text-[#C99B3B] uppercase">
                Shop
              </h4>
              <ul className="space-y-2.5 text-xs text-[#B9A8B3]">
                <li>
                  <Link to="/products" className="transition-colors duration-200 hover:text-[#F3E5AB]">
                    All Products
                  </Link>
                </li>
                <li>
                  <Link
                    to="/products?category=name-plates"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    House Name Plates
                  </Link>
                </li>
                <li>
                  <Link
                    to="/products?category=wall-clocks"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Varmala &amp; Memory Clocks
                  </Link>
                </li>
                <li>
                  <Link
                    to="/products?category=divine-pooja"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Krishna Jhulas &amp; Pooja Art
                  </Link>
                </li>
                <li>
                  <Link
                    to="/products?category=custom-keepsakes"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Ring Platters &amp; Kankotri Frames
                  </Link>
                </li>
                <li>
                  <Link to="/custom" className="transition-colors duration-200 hover:text-[#F3E5AB]">
                    Custom Order
                  </Link>
                </li>
              </ul>
            </div>

            {/* Column 3: LEARN */}
            <div>
              <h4 className="mb-4 text-[10px] font-bold tracking-[0.25em] text-[#C99B3B] uppercase">
                Learn
              </h4>
              <ul className="space-y-2.5 text-xs text-[#B9A8B3]">
                <li>
                  <Link to="/our-story" className="transition-colors duration-200 hover:text-[#F3E5AB]">
                    Our Story
                  </Link>
                </li>
                <li>
                  <Link
                    to="/our-story#process"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    How It&rsquo;s Made
                  </Link>
                </li>
                <li>
                  <Link
                    to="/products"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Design Gallery
                  </Link>
                </li>
                <li>
                  <Link
                    to="/our-story#faq"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    FAQ &amp; Flower Preservation
                  </Link>
                </li>
                <li>
                  <Link
                    to="/our-story#shipping"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Resin Care Guide
                  </Link>
                </li>
              </ul>
            </div>

            {/* Column 4: HELP & DIRECT CONTACT */}
            <div>
              <h4 className="mb-4 text-[10px] font-bold tracking-[0.25em] text-[#C99B3B] uppercase">
                Help
              </h4>
              <ul className="space-y-2.5 text-xs text-[#B9A8B3]">
                <li>
                  <Link
                    to="/our-story#shipping"
                    className="transition-colors duration-200 hover:text-[#F3E5AB]"
                  >
                    Shipping &amp; Delivery
                  </Link>
                </li>
                <li>
                  <Link to="/contact" className="transition-colors duration-200 hover:text-[#F3E5AB]">
                    Contact Us
                  </Link>
                </li>
                <li>
                  <Link to="/privacy" className="transition-colors duration-200 hover:text-[#F3E5AB]">
                    Privacy Notice
                  </Link>
                </li>
              </ul>

              <div className="mt-6 space-y-2 border-t border-[#261D29] pt-4">
                <a
                  href={`mailto:${contactEmail}`}
                  className="flex items-center gap-2 text-xs text-[#9E8C98] transition hover:text-[#F3E5AB]"
                >
                  <Mail className="h-3.5 w-3.5 shrink-0 text-[#C99B3B]" />
                  <span className="truncate">{contactEmail}</span>
                </a>
                <a
                  href={whatsAppUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-2 text-xs text-[#9E8C98] transition hover:text-[#25D366]"
                >
                  <Phone className="h-3.5 w-3.5 shrink-0 text-[#25D366]" />
                  <span>Chat on WhatsApp</span>
                </a>
                <div className="flex items-center gap-2 text-[11px] text-[#7D6C77]">
                  <MapPin className="h-3.5 w-3.5 shrink-0 text-[#C99B3B]" />
                  <span>{studioLocation}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Horizontal Divider */}
          <div className="h-px bg-[#261D29]" />

          {/* Bottom Bar: Copyright & Payment/Shipping Badges */}
          <div className="flex flex-col flex-wrap items-center justify-between gap-3 pt-6 sm:flex-row">
            <p className="text-[11px] tracking-wide text-[#7D6C77]">
              © {new Date().getFullYear()} {bizName}. All rights reserved. Made with love in
              Gujarat, India.
            </p>
            <div className="flex flex-wrap items-center gap-2">
              {['UPI', 'Razorpay', 'Cards', 'Bank Transfer', 'Worldwide Shipping'].map((badge) => (
                <span
                  key={badge}
                  className="rounded-xs border border-[#261D29] px-2.5 py-1 text-[9px] font-semibold tracking-widest text-[#7D6C77] uppercase"
                >
                  {badge}
                </span>
              ))}
            </div>
          </div>
        </div>
      </footer>

      {/* Floating WhatsApp Quick-Chat Button (Bottom Right, like dharaartcreation.com) */}
      <a
        href={whatsAppUrl}
        target="_blank"
        rel="noopener noreferrer"
        aria-label="Chat on WhatsApp"
        title="Chat directly with Dhara on WhatsApp"
        className="fixed right-6 bottom-6 z-50 flex h-14 w-14 items-center justify-center rounded-full bg-[#25D366] text-white transition-transform duration-200 hover:scale-105"
        style={{ boxShadow: '0 4px 20px rgba(37, 211, 102, 0.38)' }}
      >
        <svg width="26" height="26" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z" />
        </svg>
      </a>
    </div>
  );
};
