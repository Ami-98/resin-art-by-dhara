import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { BootstrapResponse, RegionCode, RegionDefinition } from '../../api/types';

export type RegionSource = 'manual' | 'geo' | 'ip' | 'timezone' | 'default';

interface StoredRegion {
  code: RegionCode;
  source: RegionSource;
}

const STORAGE_KEY = 'resinart.region.v1';

interface RegionContextValue {
  region: RegionCode;
  regionDef: RegionDefinition;
  regions: RegionDefinition[];
  source: RegionSource;
  bootstrap?: BootstrapResponse;
  isLoading: boolean;
  isLocating: boolean;
  locationMessage?: string | null;
  setRegionManual: (code: RegionCode) => void;
  detectWithBrowserGeolocation: () => Promise<void>;
}

const FALLBACK_REGION: RegionDefinition = {
  code: 'GJ-IN',
  name: 'Gujarat / India',
  countryCode: 'IN',
  currency: 'INR',
  locale: 'en-IN',
  flag: '🇮🇳',
  timezones: ['Asia/Kolkata'],
};

const RegionContext = createContext<RegionContextValue | undefined>(undefined);

function readStoredRegion(): StoredRegion | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredRegion;
    if (parsed?.code) return parsed;
  } catch {
    // ignore storage errors
  }
  return null;
}

function writeStoredRegion(value: StoredRegion) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  } catch {
    // ignore
  }
}

function guessFromTimezone(regions: RegionDefinition[]): RegionCode | null {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!tz) return null;
    const matched = regions.find((r) =>
      r.timezones?.some((candidate) => candidate.toLowerCase() === tz.toLowerCase())
    );
    return matched?.code ?? null;
  } catch {
    return null;
  }
}

export const RegionProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const initialStored = useMemo(() => readStoredRegion(), []);
  const [region, setRegion] = useState<RegionCode>(initialStored?.code ?? 'GJ-IN');
  const [source, setSource] = useState<RegionSource>(initialStored?.source ?? 'default');
  const [isLocating, setIsLocating] = useState(false);
  const [locationMessage, setLocationMessage] = useState<string | null>(null);

  const { data: bootstrap, isLoading } = useQuery({
    queryKey: ['bootstrap'],
    queryFn: api.getBootstrap,
    staleTime: 5 * 60 * 1000,
  });

  // First-visit auto-detection (IP -> timezone -> config default). Never overrides a manual choice.
  useEffect(() => {
    if (!bootstrap) return;
    const stored = readStoredRegion();
    const validCodes = new Set(bootstrap.regions.map((r) => r.code));

    if (stored && validCodes.has(stored.code)) {
      setRegion(stored.code);
      setSource(stored.source);
      return;
    }

    if (bootstrap.suggestedRegion && validCodes.has(bootstrap.suggestedRegion)) {
      const next: StoredRegion = { code: bootstrap.suggestedRegion, source: 'ip' };
      setRegion(next.code);
      setSource(next.source);
      writeStoredRegion(next);
      return;
    }

    const tzGuess = guessFromTimezone(bootstrap.regions);
    if (tzGuess && validCodes.has(tzGuess)) {
      const next: StoredRegion = { code: tzGuess, source: 'timezone' };
      setRegion(next.code);
      setSource(next.source);
      writeStoredRegion(next);
      return;
    }

    const fallback: StoredRegion = { code: bootstrap.defaultRegion, source: 'default' };
    setRegion(fallback.code);
    setSource(fallback.source);
    writeStoredRegion(fallback);
  }, [bootstrap]);

  const setRegionManual = (code: RegionCode) => {
    const next: StoredRegion = { code, source: 'manual' };
    setRegion(code);
    setSource('manual');
    setLocationMessage(null);
    writeStoredRegion(next);
  };

  const detectWithBrowserGeolocation = async () => {
    if (!('geolocation' in navigator)) {
      setLocationMessage('Browser location is not supported on this device.');
      return;
    }
    setIsLocating(true);
    setLocationMessage(null);
    try {
      const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: false,
          timeout: 8000,
          maximumAge: 10 * 60 * 1000,
        })
      );
      const res = await api.resolveCoordinates(pos.coords.latitude, pos.coords.longitude);
      const next: StoredRegion = { code: res.regionCode, source: 'geo' };
      setRegion(res.regionCode);
      setSource('geo');
      writeStoredRegion(next);
      setLocationMessage(
        res.matched && res.label
          ? `Detected ${res.label} → showing ${res.regionCode} pricing.`
          : `Location outside primary regions → showing ${res.regionCode} pricing.`
      );
    } catch {
      setLocationMessage('Could not access browser location. You can pick your region from the selector.');
    } finally {
      setIsLocating(false);
    }
  };

  const regions = bootstrap?.regions ?? [FALLBACK_REGION];
  const regionDef = regions.find((r) => r.code === region) ?? regions[0] ?? FALLBACK_REGION;

  return (
    <RegionContext.Provider
      value={{
        region,
        regionDef,
        regions,
        source,
        bootstrap,
        isLoading,
        isLocating,
        locationMessage,
        setRegionManual,
        detectWithBrowserGeolocation,
      }}
    >
      {children}
    </RegionContext.Provider>
  );
};

export function useRegion(): RegionContextValue {
  const ctx = useContext(RegionContext);
  if (!ctx) throw new Error('useRegion must be used inside RegionProvider');
  return ctx;
}
