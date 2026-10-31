import React, { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, RefreshCw, Save } from 'lucide-react';
import { api } from '../../api/client';
import type { PricingRulesDocument, RegionalBasePrice } from '../../api/types';

export const AdminPricingPage: React.FC = () => {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'pricing-matrix'],
    queryFn: api.getPricingMatrix,
  });

  const [rows, setRows] = useState<Record<string, Record<string, RegionalBasePrice>>>({});
  const [rules, setRules] = useState<PricingRulesDocument | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [isSavingMatrix, setIsSavingMatrix] = useState(false);
  const [isSavingRules, setIsSavingRules] = useState(false);

  useEffect(() => {
    if (!data) return;
    const map: Record<string, Record<string, RegionalBasePrice>> = {};
    for (const p of data.products) {
      map[p.id] = { ...p.basePrices };
    }
    setRows(map);
    setRules(structuredClone(data.rules));
  }, [data]);

  const handleCellChange = (
    productId: string,
    regionCode: string,
    currency: string,
    majorStr: string
  ) => {
    const parsed = Number.parseFloat(majorStr);
    const amount = Number.isFinite(parsed) ? Math.max(0, Math.round(parsed * 100)) : 0;
    setRows((prev) => ({
      ...prev,
      [productId]: {
        ...(prev[productId] ?? {}),
        [regionCode]: { amount, currency, isOverridden: true },
      },
    }));
  };

  const toggleOverride = (productId: string, regionCode: string, currency: string) => {
    setRows((prev) => {
      const cur = prev[productId]?.[regionCode];
      if (!cur) return prev;
      return {
        ...prev,
        [productId]: {
          ...prev[productId],
          [regionCode]: { ...cur, currency, isOverridden: !cur.isOverridden },
        },
      };
    });
  };

  const handleSaveMatrix = async () => {
    setIsSavingMatrix(true);
    setStatusMessage(null);
    try {
      const payload = Object.entries(rows).map(([productId, basePrices]) => ({
        productId,
        basePrices,
      }));
      const res = await api.savePricingMatrix(payload);
      setStatusMessage(`Saved regional prices for ${res.updated} products.`);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'pricing-matrix'] });
      await queryClient.invalidateQueries({ queryKey: ['products'] });
    } finally {
      setIsSavingMatrix(false);
    }
  };

  const handleSaveRules = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!rules) return;
    setIsSavingRules(true);
    setStatusMessage(null);
    try {
      await api.updatePricingRules(rules);
      setStatusMessage('Saved regional derivation rules, shipping notes & extras.');
      await queryClient.invalidateQueries({ queryKey: ['admin', 'pricing-matrix'] });
      await queryClient.invalidateQueries({ queryKey: ['bootstrap'] });
    } finally {
      setIsSavingRules(false);
    }
  };

  const handleRecalculate = async (forceAll: boolean) => {
    setStatusMessage(null);
    const res = await api.recalculatePrices(false, forceAll);
    setStatusMessage(`Recalculated ${res.count} regional prices from Gujarat (INR) base rates.`);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'pricing-matrix'] });
    await queryClient.invalidateQueries({ queryKey: ['products'] });
  };

  if (isLoading || !data || !rules) {
    return <div className="p-8 text-sm text-stone-500">Loading pricing matrix…</div>;
  }

  return (
    <div className="space-y-8">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-stone-900">
            Regional Pricing Matrix &amp; Modifiers
          </h1>
          <p className="mt-1 text-sm text-stone-600">
            Set and update standard &amp; custom base prices across Gujarat/India (INR), USA (USD)
            and Canada (CAD).
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => void handleRecalculate(true)}
            className="inline-flex items-center gap-1.5 rounded-full border border-stone-300 bg-white px-4 py-2 text-xs font-semibold text-stone-800 hover:border-amber-800"
          >
            <RefreshCw className="h-3.5 w-3.5" /> Derive US &amp; CA from INR
          </button>
          <button
            type="button"
            onClick={() => void handleSaveMatrix()}
            disabled={isSavingMatrix}
            className="inline-flex items-center gap-1.5 rounded-full bg-emerald-700 px-5 py-2.5 text-xs font-semibold text-white hover:bg-emerald-800 disabled:opacity-50"
          >
            <Save className="h-3.5 w-3.5" />
            {isSavingMatrix ? 'Saving Matrix…' : 'Save All Matrix Prices'}
          </button>
        </div>
      </div>

      {statusMessage && (
        <div className="flex items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-semibold text-emerald-900">
          <Check className="h-4 w-4 text-emerald-700" /> {statusMessage}
        </div>
      )}

      {/* Spreadsheet Matrix */}
      <div className="overflow-hidden rounded-3xl border border-stone-200 bg-white shadow-xs">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="border-b border-stone-200 bg-stone-50 text-[11px] uppercase tracking-wider text-stone-500">
              <tr>
                <th className="px-4 py-3.5">Product</th>
                <th className="px-4 py-3.5">Type</th>
                {data.regions.map((r) => (
                  <th key={r.code} className="px-4 py-3.5">
                    {r.flag} {r.name} ({r.currency})
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-200">
              {data.products.map((p) => (
                <tr key={p.id} className="hover:bg-stone-50/70">
                  <td className="px-4 py-3">
                    <div className="font-bold text-stone-900">{p.name}</div>
                    <div className="text-[11px] text-stone-500">{p.category}</div>
                  </td>
                  <td className="px-4 py-3 capitalize text-stone-600">{p.type}</td>
                  {data.regions.map((r) => {
                    const cell = rows[p.id]?.[r.code] ?? {
                      amount: 0,
                      currency: r.currency,
                      isOverridden: true,
                    };
                    return (
                      <td key={r.code} className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <input
                            type="number"
                            step="0.01"
                            min="0"
                            value={(cell.amount / 100).toFixed(2)}
                            onChange={(e) =>
                              handleCellChange(p.id, r.code, r.currency, e.target.value)
                            }
                            className="w-28 rounded-xl border border-stone-300 px-3 py-1.5 text-xs font-semibold text-stone-900 focus:border-amber-800 focus:outline-none"
                          />
                          {r.code !== rules.baseRegion && (
                            <button
                              type="button"
                              onClick={() => toggleOverride(p.id, r.code, r.currency)}
                              className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                                cell.isOverridden
                                  ? 'bg-amber-100 text-amber-900'
                                  : 'bg-stone-200 text-stone-600'
                              }`}
                              title="Toggle manual pin vs auto-derived from base region"
                            >
                              {cell.isOverridden ? 'Manual' : 'Auto'}
                            </button>
                          )}
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Regional Conversion Rules, Shipping & Tax Notes */}
      <form
        onSubmit={handleSaveRules}
        className="rounded-3xl border border-stone-200 bg-white p-6 shadow-xs sm:p-8 space-y-6"
      >
        <div className="flex items-center justify-between border-b border-stone-200 pb-4">
          <div>
            <h2 className="text-2xl font-bold text-stone-900">
              Regional Rules, Shipping Notes &amp; Tax Labels
            </h2>
            <p className="text-xs text-stone-500">
              Stored in <code>pricing-rules.json</code> · Controls exchange rates, markups, and
              regional disclosures.
            </p>
          </div>
          <button
            type="submit"
            disabled={isSavingRules}
            className="inline-flex items-center gap-1.5 rounded-full bg-stone-900 px-5 py-2.5 text-xs font-semibold text-white hover:bg-amber-900 disabled:opacity-50"
          >
            <Save className="h-3.5 w-3.5" />
            {isSavingRules ? 'Saving…' : 'Save Pricing Rules'}
          </button>
        </div>

        <div className="grid gap-6 md:grid-cols-3">
          {data.regions.map((r) => {
            const devRule = rules.regionRules[r.code] ?? {
              rate: 1,
              markupPercent: 0,
              rounding: { mode: 'none', step: 100, ending: 99 },
            };
            const shipRule = rules.shipping[r.code] ?? {
              mode: 'included',
              amount: 0,
              note: '',
            };
            const taxRule = rules.tax[r.code] ?? {
              display: 'inclusive',
              label: '',
            };

            return (
              <div
                key={r.code}
                className="space-y-3 rounded-2xl border border-stone-200 bg-stone-50 p-4"
              >
                <div className="font-bold text-stone-900">
                  {r.flag} {r.name} ({r.currency})
                </div>

                {r.code !== rules.baseRegion && (
                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="block text-[11px] font-semibold text-stone-600">
                        INR per 1 {r.currency}
                      </label>
                      <input
                        type="number"
                        step="0.1"
                        value={devRule.rate}
                        onChange={(e) =>
                          setRules({
                            ...rules,
                            regionRules: {
                              ...rules.regionRules,
                              [r.code]: { ...devRule, rate: Number(e.target.value) },
                            },
                          })
                        }
                        className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-2.5 py-1.5 text-xs"
                      />
                    </div>
                    <div>
                      <label className="block text-[11px] font-semibold text-stone-600">
                        Export Markup %
                      </label>
                      <input
                        type="number"
                        step="1"
                        value={devRule.markupPercent}
                        onChange={(e) =>
                          setRules({
                            ...rules,
                            regionRules: {
                              ...rules.regionRules,
                              [r.code]: { ...devRule, markupPercent: Number(e.target.value) },
                            },
                          })
                        }
                        className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-2.5 py-1.5 text-xs"
                      />
                    </div>
                  </div>
                )}

                <div>
                  <label className="block text-[11px] font-semibold text-stone-600">
                    Shipping Note Shown to Customers
                  </label>
                  <input
                    type="text"
                    value={shipRule.note}
                    onChange={(e) =>
                      setRules({
                        ...rules,
                        shipping: {
                          ...rules.shipping,
                          [r.code]: { ...shipRule, note: e.target.value },
                        },
                      })
                    }
                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-2.5 py-1.5 text-xs"
                  />
                </div>

                <div>
                  <label className="block text-[11px] font-semibold text-stone-600">
                    Tax / Duty Disclosure Label
                  </label>
                  <input
                    type="text"
                    value={taxRule.label}
                    onChange={(e) =>
                      setRules({
                        ...rules,
                        tax: {
                          ...rules.tax,
                          [r.code]: { ...taxRule, label: e.target.value },
                        },
                      })
                    }
                    className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-2.5 py-1.5 text-xs"
                  />
                </div>
              </div>
            );
          })}
        </div>
      </form>
    </div>
  );
};
