import React from 'react';

/**
 * Vector emblem & lockup distilled from @resin_delights_by_dhara's Instagram identity:
 * - Outer swirling rose-pink & amethyst silk resin ribbons
 * - Metallic 24K gold geode deckled inner ring
 * - Deep obsidian-plum core with alcohol-ink amethyst & gold veins
 * - Playfair Display monogram & botanical rose accent
 */
export const BrandLogoMark: React.FC<{ className?: string }> = ({ className = 'h-11 w-11' }) => (
  <svg
    viewBox="0 0 120 120"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    className={className}
    aria-hidden="true"
  >
    <defs>
      <linearGradient id="dharaRoseSwirl" x1="0" y1="0" x2="120" y2="120" gradientUnits="userSpaceOnUse">
        <stop offset="0%" stopColor="#F4C2D7" />
        <stop offset="35%" stopColor="#C86D8A" />
        <stop offset="70%" stopColor="#5E3758" />
        <stop offset="100%" stopColor="#E8A5B8" />
      </linearGradient>
      <linearGradient id="dharaGoldFoil" x1="15" y1="15" x2="105" y2="105" gradientUnits="userSpaceOnUse">
        <stop offset="0%" stopColor="#F6E06E" />
        <stop offset="45%" stopColor="#C99B3B" />
        <stop offset="75%" stopColor="#FFF1B8" />
        <stop offset="100%" stopColor="#9A7020" />
      </linearGradient>
      <radialGradient id="dharaObsidianCore" cx="50%" cy="50%" r="50%">
        <stop offset="0%" stopColor="#2E1C36" />
        <stop offset="55%" stopColor="#161118" />
        <stop offset="100%" stopColor="#0B080D" />
      </radialGradient>
      <radialGradient id="dharaAmethystCloud" cx="62%" cy="42%" r="48%">
        <stop offset="0%" stopColor="#8E5B88" stopOpacity="0.65" />
        <stop offset="55%" stopColor="#5E3758" stopOpacity="0.3" />
        <stop offset="100%" stopColor="#161118" stopOpacity="0" />
      </radialGradient>
    </defs>

    {/* Outer swirling rose-mauve silk ribbon ring */}
    <circle cx="60" cy="60" r="57" fill="url(#dharaRoseSwirl)" />
    <path
      d="M60 5C90.376 5 115 29.624 115 60C115 90.376 90.376 115 60 115C29.624 115 5 90.376 5 60C5 29.624 29.624 5 60 5Z"
      stroke="#FCE4EE"
      strokeWidth="1.2"
      strokeOpacity="0.55"
    />
    {/* Organic fluid swirl arcs */}
    <path
      d="M18 36C32 12 68 6 94 22C114 35 117 67 101 90"
      stroke="#FCE4EE"
      strokeWidth="2"
      strokeLinecap="round"
      strokeOpacity="0.5"
    />
    <path
      d="M102 84C88 108 52 114 26 98C6 85 3 53 19 30"
      stroke="#4A203B"
      strokeWidth="2.2"
      strokeLinecap="round"
      strokeOpacity="0.45"
    />

    {/* Inner obsidian-plum resin disc with 24K gold geode rim */}
    <circle
      cx="60"
      cy="60"
      r="46"
      fill="url(#dharaObsidianCore)"
      stroke="url(#dharaGoldFoil)"
      strokeWidth="2.5"
    />

    {/* Alcohol-ink amethyst & gold dust cloud inside core */}
    <circle cx="60" cy="60" r="43" fill="url(#dharaAmethystCloud)" />
    <path
      d="M34 76C45 64 56 69 68 54C76 44 80 34 89 31"
      stroke="url(#dharaGoldFoil)"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeDasharray="2 3"
      strokeOpacity="0.75"
    />

    {/* Monogram 'RD' in Playfair Display + gold italic 'Dhara' */}
    <text
      x="60"
      y="56"
      textAnchor="middle"
      fill="#F4C2D7"
      fontFamily="'Playfair Display', Georgia, serif"
      fontWeight="700"
      fontSize="24"
      letterSpacing="1"
    >
      RD
    </text>
    <text
      x="60"
      y="80"
      textAnchor="middle"
      fill="url(#dharaGoldFoil)"
      fontFamily="'Playfair Display', Georgia, serif"
      fontStyle="italic"
      fontWeight="600"
      fontSize="17"
    >
      Dhara
    </text>

    {/* Subtle crimson rose bud accent at top crest */}
    <circle cx="60" cy="22" r="3" fill="#D93854" />
    <circle cx="60" cy="22" r="1.3" fill="#F6E06E" />
  </svg>
);

/**
 * Signature botanical rose + filigree flourish inspired by the bottom watermark
 * on @resin_delights_by_dhara's Instagram photography.
 */
export const RoseWatermarkBadge: React.FC<{ className?: string; light?: boolean }> = ({
  className = '',
  light = false,
}) => (
  <div className={`inline-flex items-center gap-2 ${className}`}>
    <svg viewBox="0 0 36 28" className="h-5 w-6 shrink-0" fill="none" aria-hidden="true">
      {/* Filigree vine leaves */}
      <path
        d="M4 18C9 15 13 15 18 17M8 12C11 10 15 11 17 14M9 22C12 22 15 20 18 17"
        stroke={light ? '#E5C158' : '#5E3758'}
        strokeWidth="1.3"
        strokeLinecap="round"
      />
      <ellipse cx="9" cy="12" rx="3" ry="1.5" transform="rotate(-25 9 12)" fill="#1E6B52" />
      <ellipse cx="10" cy="21" rx="3" ry="1.5" transform="rotate(20 10 21)" fill="#1E6B52" />
      {/* Crimson rose blossom */}
      <circle cx="22" cy="16" r="6" fill="#B8293D" />
      <path
        d="M19 15C20 12 24 12 25 15C26 18 22 20 20 18C18 16 21 14 22 16"
        stroke="#F4C2D7"
        strokeWidth="1.1"
        strokeLinecap="round"
      />
      {/* Gold sparkle dots */}
      <circle cx="31" cy="11" r="1.2" fill="#C99B3B" />
      <circle cx="30" cy="20" r="1" fill="#C99B3B" />
    </svg>
    <span
      className={`font-serif-display text-base italic font-medium leading-none tracking-wide ${
        light ? 'text-[#F3E5AB]' : 'text-[#5E3758]'
      }`}
    >
      Resin delights by Dhara
    </span>
  </div>
);

/**
 * Symmetrical gold scroll divider inspired by the 'Aviraj' & 'Krupa Bhavan' black-and-gold nameplates.
 */
export const GoldScrollDivider: React.FC<{ className?: string }> = ({ className = '' }) => (
  <div className={`flex items-center justify-center gap-3 ${className}`} aria-hidden="true">
    <span className="h-px w-12 bg-gradient-to-r from-transparent via-[#C99B3B]/70 to-[#C99B3B]" />
    <svg viewBox="0 0 28 16" className="h-3.5 w-6 text-[#C99B3B]" fill="currentColor">
      <path d="M14 1L16.8 6.2L22.5 8L16.8 9.8L14 15L11.2 9.8L5.5 8L11.2 6.2L14 1Z" />
      <circle cx="2" cy="8" r="1.5" />
      <circle cx="26" cy="8" r="1.5" />
    </svg>
    <span className="h-px w-12 bg-gradient-to-l from-transparent via-[#C99B3B]/70 to-[#C99B3B]" />
  </div>
);
