import React from 'react';
import { Link } from 'react-router-dom';
import { Clock, Sparkles } from 'lucide-react';
import type { ProductSummaryDto } from '../../api/types';
import { formatMoney } from '../../lib/money';
import { useRegion } from '../region/RegionContext';

export const ProductCard: React.FC<{ product: ProductSummaryDto }> = ({ product }) => {
  const { regionDef } = useRegion();
  const detailPath = `/products/${product.slug}`;

  return (
    <Link
      to={detailPath}
      className="group flex flex-col overflow-hidden rounded-2xl border border-[#EADCE0] bg-white shadow-luxe transition duration-300 hover:-translate-y-1 hover:border-[#C99B3B]"
    >
      <div className="relative aspect-square overflow-hidden bg-[#F6EEF0]">
        <img
          src={product.primaryImageUrl}
          alt={product.primaryImageAlt}
          loading="lazy"
          className="h-full w-full object-cover transition duration-500 group-hover:scale-105"
        />
        <div className="absolute top-3 left-3 flex flex-wrap gap-1.5">
          {product.type === 'custom' && (
            <span className="inline-flex items-center gap-1 rounded-full border border-[#C99B3B]/50 bg-[#161118]/80 px-2.5 py-1 text-[11px] font-bold tracking-wide text-[#F3E5AB] backdrop-blur-xs">
              <Sparkles className="h-3 w-3 text-[#C99B3B]" /> Made to Order
            </span>
          )}
          {product.hasCustomOptions && product.type === 'standard' && (
            <span className="rounded-full border border-[#D98A9E]/50 bg-white/90 px-2.5 py-1 text-[11px] font-bold text-[#5E3758] shadow-2xs backdrop-blur-xs">
              Customizable
            </span>
          )}
        </div>
      </div>

      <div className="flex flex-1 flex-col justify-between p-4 sm:p-5">
        <div>
          <div className="flex items-center justify-between gap-2 text-xs text-[#6E5A68]">
            <span className="font-bold uppercase tracking-wider text-[#9A7020]">
              {product.category.replace('-', ' ')}
            </span>
            <span className="inline-flex items-center gap-1">
              <Clock className="h-3 w-3 text-[#C86D8A]" /> Ready in ~{product.leadTimeDays}d
            </span>
          </div>
          <h3 className="mt-1.5 text-lg font-bold leading-snug text-[#161118] transition group-hover:text-[#5E3758]">
            {product.name}
          </h3>
          <p className="mt-1.5 line-clamp-2 text-sm leading-relaxed text-[#4A3E47]">
            {product.shortDescription}
          </p>
        </div>

        <div className="mt-4 flex items-end justify-between border-t border-[#F6EEF0] pt-3">
          <div>
            <span className="block text-[11px] uppercase tracking-wider text-[#6E5A68]">
              {product.hasCustomOptions ? 'Starting at' : 'Price'}
            </span>
            <span className="tabular-price text-lg font-bold text-[#161118]">
              {formatMoney(product.fromPrice.amount, product.fromPrice.currency, regionDef.locale)}
            </span>
          </div>
          <span className="text-xs font-bold text-[#5E3758] transition group-hover:translate-x-0.5 group-hover:text-[#9A7020]">
            View &amp; Customize →
          </span>
        </div>
      </div>
    </Link>
  );
};
