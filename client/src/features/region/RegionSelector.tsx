import React from 'react';
import { Globe, RotateCcw } from 'lucide-react';
import { useRegion } from './RegionContext';

export const RegionSelector: React.FC = () => {
  const { region, regions, source, setRegionManual, resetToAutoRegion } = useRegion();

  return (
    <div className="flex items-center gap-1.5">
      <div className="relative flex items-center">
        <Globe className="pointer-events-none absolute left-2.5 h-3.5 w-3.5 text-amber-800" />
        <select
          aria-label="Admin preview region"
          title="Admin only: Preview storefront pricing region"
          value={region}
          onChange={(e) => setRegionManual(e.target.value)}
          className="rounded-full border border-stone-300 bg-white py-1.5 pl-8 pr-7 text-xs font-medium text-stone-800 transition hover:border-amber-800 focus:border-amber-800 focus:outline-none"
        >
          {regions.map((r) => (
            <option key={r.code} value={r.code}>
              {r.flag ? `${r.flag} ` : ''}
              {r.name} ({r.currency})
            </option>
          ))}
        </select>
      </div>
      {source === 'manual' && (
        <button
          type="button"
          onClick={resetToAutoRegion}
          title="Reset to automatic visitor location detection"
          className="inline-flex items-center gap-1 rounded-full border border-amber-300 bg-amber-50 px-2.5 py-1.5 text-xs font-medium text-amber-900 transition hover:bg-amber-100"
        >
          <RotateCcw className="h-3 w-3" />
          <span className="hidden sm:inline">Auto</span>
        </button>
      )}
    </div>
  );
};

