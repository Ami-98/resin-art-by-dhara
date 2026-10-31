import React, { useState } from 'react';
import { Globe, LocateFixed, X } from 'lucide-react';
import { useRegion } from './RegionContext';

export const RegionSelector: React.FC = () => {
  const { region, regions, setRegionManual, detectWithBrowserGeolocation, isLocating } = useRegion();

  return (
    <div className="flex items-center gap-1.5">
      <div className="relative flex items-center">
        <Globe className="pointer-events-none absolute left-2.5 h-3.5 w-3.5 text-[#9A7020]" />
        <select
          aria-label="Select pricing region"
          value={region}
          onChange={(e) => setRegionManual(e.target.value)}
          className="rounded-full border border-[#EADCE0] bg-white py-1.5 pl-8 pr-7 text-xs font-medium text-[#161118] shadow-2xs transition hover:border-[#C99B3B] focus:border-[#5E3758] focus:outline-none"
        >
          {regions.map((r) => (
            <option key={r.code} value={r.code}>
              {r.flag ? `${r.flag} ` : ''}
              {r.name} ({r.currency})
            </option>
          ))}
        </select>
      </div>
      <button
        type="button"
        onClick={() => void detectWithBrowserGeolocation()}
        disabled={isLocating}
        title="Detect my region using browser location"
        className="inline-flex items-center gap-1 rounded-full border border-[#EADCE0] bg-white px-2.5 py-1.5 text-xs font-medium text-[#4A3E47] transition hover:border-[#C99B3B] hover:text-[#5E3758] disabled:opacity-50"
      >
        <LocateFixed className={`h-3.5 w-3.5 text-[#C86D8A] ${isLocating ? 'animate-spin' : ''}`} />
        <span className="hidden sm:inline">{isLocating ? 'Detecting…' : 'Locate'}</span>
      </button>
    </div>
  );
};

export const RegionBanner: React.FC = () => {
  const { regionDef, regions, source, bootstrap, setRegionManual, locationMessage } = useRegion();
  const [dismissed, setDismissed] = useState(false);

  if (dismissed || bootstrap?.features.showRegionBanner === false) return null;

  const sourceLabel =
    source === 'manual'
      ? 'Selected region'
      : source === 'geo'
      ? 'Detected from your location'
      : source === 'ip'
      ? 'Auto-detected for your country'
      : source === 'timezone'
      ? 'Auto-detected from your timezone'
      : 'Regional store';

  return (
    <div className="border-b border-[#EADCE0] bg-gradient-to-r from-[#F6EEF0] via-[#FAF7F4] to-[#FBF4E6] text-xs text-[#4A3E47]">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-2 px-4 py-1.5 sm:px-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium text-[#161118]">
            {regionDef.flag} Showing estimated prices for {regionDef.name} ({regionDef.currency})
          </span>
          <span className="text-[#6E5A68]">· {sourceLabel}</span>
          {locationMessage && <span className="font-medium text-[#5E3758]">· {locationMessage}</span>}
        </div>
        <div className="flex items-center gap-2">
          <span className="hidden text-[#6E5A68] md:inline">Switch:</span>
          {regions
            .filter((r) => r.code !== regionDef.code)
            .map((r) => (
              <button
                key={r.code}
                type="button"
                onClick={() => setRegionManual(r.code)}
                className="font-semibold text-[#5E3758] underline decoration-[#C86D8A]/60 underline-offset-2 hover:text-[#9A7020]"
              >
                {r.flag} {r.name}
              </button>
            ))}
          <button
            type="button"
            onClick={() => setDismissed(true)}
            aria-label="Dismiss region notice"
            className="ml-1 rounded p-0.5 text-[#6E5A68] hover:bg-[#EADCE0]/60 hover:text-[#161118]"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </div>
  );
};
