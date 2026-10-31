import React from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Search, Sparkles } from 'lucide-react';
import { api } from '../../api/client';
import type { ProductType } from '../../api/types';
import { useRegion } from '../region/RegionContext';
import { ProductCard } from './ProductCard';

export const ProductGallery: React.FC<{ fixedType?: ProductType }> = ({ fixedType }) => {
  const { region, bootstrap } = useRegion();
  const [searchParams, setSearchParams] = useSearchParams();

  const category = searchParams.get('category') ?? '';
  const q = searchParams.get('q') ?? '';
  const typeParam = (fixedType ?? (searchParams.get('type') as ProductType | null)) || undefined;

  const { data: categories } = useQuery({
    queryKey: ['categories'],
    queryFn: api.getCategories,
    staleTime: 5 * 60 * 1000,
  });

  const { data, isLoading } = useQuery({
    queryKey: ['products', region, category, typeParam ?? 'all', q],
    queryFn: () =>
      api.getProducts({
        region,
        category: category || undefined,
        type: typeParam,
        q: q || undefined,
      }),
  });

  const shippingNote = bootstrap?.shipping?.[region]?.note;
  const taxNote = bootstrap?.tax?.[region]?.label;

  const updateFilter = (key: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    setSearchParams(next, { replace: true });
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="flex flex-col justify-between gap-4 border-b border-[#EADCE0] pb-6 md:flex-row md:items-end">
        <div>
          <span className="text-xs font-bold uppercase tracking-[0.16em] text-[#9A7020]">
            {fixedType === 'custom' ? 'Made-to-Order Designs' : 'Handmade Resin Collection'}
          </span>
          <h1 className="mt-1 text-3xl font-bold text-[#161118] sm:text-4xl">
            {fixedType === 'custom'
              ? 'Custom Nameplates & Memory Gifts'
              : 'All Handmade Pieces'}
          </h1>
          <p className="mt-2 max-w-2xl text-sm text-[#4A3E47]">
            Every piece is handmade to order in our Nadiad, Gujarat studio
            {shippingNote ? ` · ${shippingNote}` : ''}.
            {taxNote ? ` (${taxNote})` : ''}
          </p>
        </div>

        <div className="relative w-full md:w-72">
          <Search className="pointer-events-none absolute top-2.5 left-3 h-4 w-4 text-[#C86D8A]" />
          <input
            type="search"
            value={q}
            onChange={(e) => updateFilter('q', e.target.value)}
            placeholder="Search nameplates, clocks, jhulas…"
            className="w-full rounded-full border border-[#EADCE0] bg-white py-2 pr-4 pl-9 text-sm text-[#161118] shadow-2xs focus:border-[#5E3758] focus:outline-none"
          />
        </div>
      </div>

      {/* Category pills */}
      <div className="mt-6 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => updateFilter('category', '')}
          className={`rounded-full px-4 py-1.5 text-xs font-bold transition ${
            !category
              ? 'border border-[#C99B3B]/60 bg-obsidian-luxe text-[#FAF7F4] shadow-2xs'
              : 'border border-[#EADCE0] bg-white text-[#4A3E47] hover:border-[#C99B3B] hover:text-[#5E3758]'
          }`}
        >
          All Categories
        </button>
        {categories?.map((c) => (
          <button
            key={c.id}
            type="button"
            onClick={() => updateFilter('category', c.slug)}
            className={`rounded-full px-4 py-1.5 text-xs font-bold transition ${
              category === c.slug
                ? 'border border-[#C99B3B]/60 bg-[#5E3758] text-[#FAF7F4] shadow-2xs'
                : 'border border-[#EADCE0] bg-white text-[#4A3E47] hover:border-[#C99B3B] hover:text-[#5E3758]'
            }`}
          >
            {c.name}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div className="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3, 4, 5, 6].map((n) => (
            <div
              key={n}
              className="h-96 animate-pulse rounded-2xl border border-[#EADCE0] bg-[#F6EEF0]"
            />
          ))}
        </div>
      ) : data && data.items.length > 0 ? (
        <div className="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map((p) => (
            <ProductCard key={p.id} product={p} />
          ))}
        </div>
      ) : (
        <div className="mt-12 rounded-2xl border border-dashed border-[#D98A9E] bg-white p-12 text-center shadow-luxe">
          <Sparkles className="mx-auto h-8 w-8 text-[#9A7020]" />
          <h3 className="mt-3 text-xl font-bold text-[#161118]">No matching pieces found</h3>
          <p className="mt-1 text-sm text-[#4A3E47]">
            Try clearing the category or search filter, or message us on WhatsApp for a custom design.
          </p>
          <button
            type="button"
            onClick={() => setSearchParams({})}
            className="mt-4 rounded-full border border-[#C99B3B]/50 bg-obsidian-luxe px-5 py-2 text-xs font-bold text-white hover:text-[#F3E5AB]"
          >
            Show all designs
          </button>
        </div>
      )}
    </div>
  );
};
