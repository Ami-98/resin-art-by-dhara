import React from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { Instagram, MessageCircle } from 'lucide-react';
import { useRegion } from '../features/region/RegionContext';
import { RegionBanner, RegionSelector } from '../features/region/RegionSelector';
import { BrandLogoMark, RoseWatermarkBadge } from './BrandLogo';

export const AppLayout: React.FC = () => {
  const { bootstrap, regionDef } = useRegion();
  const bizName = bootstrap?.business.name ?? 'Resin Delights by Dhara';
  const tagline = bootstrap?.business.tagline ?? 'Nadiad, Gujarat · Preserving Memories in Resin';
  const studioLocation = bootstrap?.business.location ?? 'Nadiad & Uttarsanda, Gujarat, India';
  const instagramUrl =
    bootstrap?.business.social?.instagram || 'https://www.instagram.com/resin_delights_by_dhara/';

  const navClass = ({ isActive }: { isActive: boolean }) =>
    `relative text-sm font-medium transition py-1 ${
      isActive
        ? 'text-[#5E3758] font-semibold after:absolute after:bottom-0 after:left-0 after:right-0 after:h-0.5 after:rounded-full after:bg-gradient-to-r after:from-[#C86D8A] after:to-[#C99B3B]'
        : 'text-[#4A3E47] hover:text-[#5E3758]'
    }`;

  return (
    <div className="flex min-h-screen flex-col bg-[#FAF7F4] text-[#161118]">
      <RegionBanner />

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
                <span className="font-serif-display text-base italic font-semibold text-[#9A7020] sm:text-lg">
                  by Dhara
                </span>
              </div>
              <span className="block text-[10px] font-bold uppercase tracking-[0.14em] text-[#6E5A68]">
                {tagline}
              </span>
            </div>
          </Link>

          <nav className="hidden items-center gap-7 md:flex">
            <NavLink to="/" end className={navClass}>
              Home
            </NavLink>
            <NavLink to="/products" className={navClass}>
              Product Gallery
            </NavLink>
            <NavLink to="/custom" className={navClass}>
              Custom Orders
            </NavLink>
            <NavLink to="/contact" className={navClass}>
              Contact
            </NavLink>
          </nav>

          <div className="flex items-center gap-2">
            <RegionSelector />
            {bootstrap?.business.whatsAppUrl && (
              <a
                href={bootstrap.business.whatsAppUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="hidden items-center gap-1.5 rounded-full border border-[#C99B3B]/40 bg-[#146348] px-3.5 py-1.5 text-xs font-semibold text-white shadow-2xs transition hover:bg-[#0F4F39] sm:inline-flex"
              >
                <MessageCircle className="h-3.5 w-3.5 text-[#F3E5AB]" /> WhatsApp
              </a>
            )}
          </div>
        </div>
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="mt-16 border-t border-[#EADCE0] bg-obsidian-luxe text-[#F6EEF0]">
        <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
          <div className="flex flex-col items-start justify-between gap-6 sm:flex-row sm:items-center">
            <div className="flex items-center gap-3.5">
              <img
                src="/uploads/seed/dhara-logo.jpg"
                alt={bizName}
                className="h-12 w-12 rounded-full border-2 border-[#C99B3B] object-cover shadow-md"
              />
              <div>
                <RoseWatermarkBadge light />
                <p className="mt-1 text-xs text-[#D5C5CE]">
                  Handcrafted in {studioLocation} · Active pricing region: {regionDef.flag}{' '}
                  {regionDef.name} ({regionDef.currency})
                </p>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-5 text-xs text-[#E5D7DF]">
              <a
                href={instagramUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 rounded-full border border-[#D98A9E]/40 bg-[#5E3758]/40 px-3.5 py-1.5 font-semibold text-[#F4C2D7] transition hover:border-[#C99B3B] hover:text-[#F3E5AB]"
              >
                <Instagram className="h-3.5 w-3.5" /> @resin_delights_by_dhara
              </a>
              <Link to="/products" className="hover:text-[#F3E5AB]">
                Gallery
              </Link>
              <Link to="/custom" className="hover:text-[#F3E5AB]">
                Custom Requests
              </Link>
              <Link to="/contact" className="hover:text-[#F3E5AB]">
                Contact
              </Link>
              <a href="/sitemap.xml" className="hover:text-[#F3E5AB]">
                Sitemap
              </a>
              <Link to="/admin" className="hover:text-[#F3E5AB]">
                Studio Admin
              </Link>
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
};
