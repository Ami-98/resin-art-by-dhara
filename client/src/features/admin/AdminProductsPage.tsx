import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  CheckCircle2,
  Edit3,
  Plus,
  Star,
  Trash2,
  Upload,
  X,
} from 'lucide-react';
import { api } from '../../api/client';
import type {
  AdminProductDocument,
  AdminProductOption,
  OptionType,
  ProductStatus,
  ProductType,
} from '../../api/types';
import { formatMoney } from '../../lib/money';
import { useRegion } from '../region/RegionContext';

export const AdminProductsPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { regions, bootstrap } = useRegion();

  const { data: products, isLoading } = useQuery({
    queryKey: ['admin', 'products'],
    queryFn: () => api.getAdminProducts(),
  });

  const { data: categories } = useQuery({
    queryKey: ['categories'],
    queryFn: api.getCategories,
  });

  const [editing, setEditing] = useState<Partial<AdminProductDocument> | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const defaultCategory =
    categories?.[0]?.slug ?? bootstrap?.catalog?.defaultCategory ?? 'wall-clocks';
  const defaultLeadTimeDays = bootstrap?.catalog?.defaultLeadTimeDays ?? 7;

  const openNewProduct = () => {
    setError(null);
    const initialBasePrices: Record<string, { amount: number; currency: string; isOverridden: boolean }> = {};
    for (const r of regions) {
      initialBasePrices[r.code] = {
        amount: r.currency === 'INR' ? 249900 : r.currency === 'USD' ? 5499 : 7299,
        currency: r.currency,
        isOverridden: true,
      };
    }

    setEditing({
      name: '',
      slug: '',
      category: defaultCategory,
      type: 'standard',
      status: 'published',
      shortDescription: '',
      description: '',
      leadTimeDays: defaultLeadTimeDays,
      isFeatured: false,
      tags: ['handmade'],
      images: [],
      basePrices: initialBasePrices,
      options: [],
    });
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    setError(null);
    setIsSaving(true);
    try {
      if (editing.id) {
        const saved = await api.updateAdminProduct(editing.id, editing);
        setEditing(saved);
      } else {
        const created = await api.createAdminProduct(editing);
        setEditing(created);
      }
      await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
      await queryClient.invalidateQueries({ queryKey: ['products'] });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save product.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleImageUpload = async (files: FileList | null) => {
    if (!files || files.length === 0 || !editing?.id) return;
    setError(null);
    try {
      const updated = await api.uploadAdminProductImage(editing.id, files[0], editing.name);
      setEditing(updated);
      await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
      await queryClient.invalidateQueries({ queryKey: ['products'] });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Image upload failed.');
    }
  };

  const handleSetPrimary = async (imageId: string) => {
    if (!editing?.id) return;
    const updated = await api.setAdminProductPrimaryImage(editing.id, imageId);
    setEditing(updated);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
  };

  const handleRemoveImage = async (imageId: string) => {
    if (!editing?.id) return;
    const updated = await api.deleteAdminProductImage(editing.id, imageId);
    setEditing(updated);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
  };

  const handleQuickStatus = async (product: AdminProductDocument, status: ProductStatus) => {
    await api.updateAdminProductStatus(product.id, status);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
    await queryClient.invalidateQueries({ queryKey: ['products'] });
  };

  const handleDelete = async (product: AdminProductDocument, hard: boolean) => {
    await api.deleteAdminProduct(product.id, hard);
    if (editing?.id === product.id) setEditing(null);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
    await queryClient.invalidateQueries({ queryKey: ['products'] });
  };

  const setMajorPrice = (regionCode: string, currency: string, majorVal: string) => {
    if (!editing) return;
    const num = Number.parseFloat(majorVal);
    const amount = Number.isFinite(num) ? Math.max(0, Math.round(num * 100)) : 0;
    setEditing({
      ...editing,
      basePrices: {
        ...(editing.basePrices ?? {}),
        [regionCode]: { amount, currency, isOverridden: true },
      },
    });
  };

  const addOption = (type: OptionType) => {
    if (!editing) return;
    const nextOpt: AdminProductOption = {
      id: `opt_${Date.now().toString(36)}`,
      label: type === 'text' ? 'Custom Name / Engraving' : type === 'image' ? 'Reference Photo' : 'Size / Finish',
      type,
      required: false,
      choices:
        type === 'select'
          ? [
              { id: 'standard', label: 'Standard' },
              {
                id: 'large',
                label: 'Large',
                modifier: {
                  kind: 'fixed',
                  values: { 'GJ-IN': 50000, US: 1200, CA: 1600 },
                  value: 0,
                  derived: false,
                },
              },
            ]
          : [],
      modifier:
        type !== 'select'
          ? {
              kind: 'fixed',
              values: { 'GJ-IN': 15000, US: 500, CA: 700 },
              value: 0,
              derived: false,
            }
          : null,
    };
    setEditing({
      ...editing,
      options: [...(editing.options ?? []), nextOpt],
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-stone-900">Product Catalogue Management</h1>
          <p className="mt-1 text-sm text-stone-600">
            Create, edit or archive resin art products, upload multiple photos, and configure
            customization options.
          </p>
        </div>
        <button
          type="button"
          onClick={openNewProduct}
          className="inline-flex items-center gap-2 rounded-full bg-amber-900 px-5 py-2.5 text-xs font-semibold text-white hover:bg-amber-800"
        >
          <Plus className="h-4 w-4" /> New Product
        </button>
      </div>

      {/* Product Editor Drawer / Card */}
      {editing && (
        <div className="rounded-3xl border border-amber-300 bg-white p-6 shadow-md sm:p-8">
          <div className="flex items-center justify-between border-b border-stone-200 pb-4">
            <div>
              <span className="text-xs font-semibold uppercase tracking-wider text-amber-800">
                {editing.id ? `Editing ${editing.id} (v${editing.version ?? 1})` : 'Create New Product'}
              </span>
              <h2 className="text-2xl font-bold text-stone-900">
                {editing.name || 'Untitled Resin Piece'}
              </h2>
            </div>
            <button
              type="button"
              onClick={() => setEditing(null)}
              className="rounded-full p-1.5 text-stone-500 hover:bg-stone-100"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          {error && (
            <div className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-xs font-medium text-red-700">
              {error}
            </div>
          )}

          <form onSubmit={handleSave} className="mt-6 space-y-6">
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="sm:col-span-2">
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Product Name *
                </label>
                <input
                  type="text"
                  required
                  value={editing.name ?? ''}
                  onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Category
                </label>
                <select
                  value={editing.category ?? 'wall-clocks'}
                  onChange={(e) => setEditing({ ...editing, category: e.target.value })}
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                >
                  {categories?.map((c) => (
                    <option key={c.id} value={c.slug}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-4">
              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Product Type
                </label>
                <select
                  value={editing.type ?? 'standard'}
                  onChange={(e) =>
                    setEditing({ ...editing, type: e.target.value as ProductType })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                >
                  <option value="standard">Standard Catalogue</option>
                  <option value="custom">Custom Commission</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Status
                </label>
                <select
                  value={editing.status ?? 'published'}
                  onChange={(e) =>
                    setEditing({ ...editing, status: e.target.value as ProductStatus })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                >
                  <option value="published">Published</option>
                  <option value="draft">Draft</option>
                  <option value="archived">Archived</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Lead Time (days)
                </label>
                <input
                  type="number"
                  min={1}
                  max={60}
                  value={editing.leadTimeDays ?? 7}
                  onChange={(e) =>
                    setEditing({ ...editing, leadTimeDays: Number(e.target.value) })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>

              <div className="flex items-end pb-2">
                <label className="flex items-center gap-2 text-xs font-semibold text-stone-800">
                  <input
                    type="checkbox"
                    checked={Boolean(editing.isFeatured)}
                    onChange={(e) =>
                      setEditing({ ...editing, isFeatured: e.target.checked })
                    }
                    className="h-4 w-4 accent-amber-800"
                  />
                  Featured on Home Page
                </label>
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase text-stone-700">
                Short Summary
              </label>
              <input
                type="text"
                value={editing.shortDescription ?? ''}
                onChange={(e) => setEditing({ ...editing, shortDescription: e.target.value })}
                className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase text-stone-700">
                Full Craft Description
              </label>
              <textarea
                rows={3}
                value={editing.description ?? ''}
                onChange={(e) => setEditing({ ...editing, description: e.target.value })}
                className="mt-1 w-full rounded-xl border border-stone-300 p-3 text-sm"
              />
            </div>

            {/* Regional Base Prices */}
            <div className="rounded-2xl border border-stone-200 bg-stone-50 p-4">
              <span className="text-xs font-bold uppercase tracking-wider text-stone-800">
                Regional Base Prices (Major Units)
              </span>
              <div className="mt-3 grid gap-4 sm:grid-cols-3">
                {regions.map((r) => {
                  const minor = editing.basePrices?.[r.code]?.amount ?? 0;
                  return (
                    <div key={r.code}>
                      <label className="block text-xs font-medium text-stone-700">
                        {r.flag} {r.name} ({r.currency})
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        value={(minor / 100).toFixed(2)}
                        onChange={(e) => setMajorPrice(r.code, r.currency, e.target.value)}
                        className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-sm font-semibold"
                      />
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Customization Options Builder */}
            <div className="rounded-2xl border border-stone-200 p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="text-xs font-bold uppercase tracking-wider text-stone-800">
                  Customization Options ({editing.options?.length ?? 0})
                </span>
                <div className="flex flex-wrap gap-1.5">
                  <button
                    type="button"
                    onClick={() => addOption('select')}
                    className="rounded-full border border-stone-300 px-3 py-1 text-xs font-semibold hover:border-amber-800"
                  >
                    + Dropdown Choice
                  </button>
                  <button
                    type="button"
                    onClick={() => addOption('text')}
                    className="rounded-full border border-stone-300 px-3 py-1 text-xs font-semibold hover:border-amber-800"
                  >
                    + Custom Text
                  </button>
                  <button
                    type="button"
                    onClick={() => addOption('image')}
                    className="rounded-full border border-stone-300 px-3 py-1 text-xs font-semibold hover:border-amber-800"
                  >
                    + Customer Photo
                  </button>
                </div>
              </div>

              {editing.options && editing.options.length > 0 && (
                <div className="mt-3 space-y-2">
                  {editing.options.map((opt, idx) => (
                    <div
                      key={opt.id}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-stone-200 bg-stone-50 px-3 py-2 text-xs"
                    >
                      <div className="flex flex-1 items-center gap-2">
                        <span className="rounded bg-amber-100 px-2 py-0.5 font-semibold text-amber-900 uppercase">
                          {opt.type}
                        </span>
                        <input
                          type="text"
                          value={opt.label}
                          onChange={(e) => {
                            const next = [...(editing.options ?? [])];
                            next[idx] = { ...opt, label: e.target.value };
                            setEditing({ ...editing, options: next });
                          }}
                          className="flex-1 rounded border border-stone-300 bg-white px-2.5 py-1 text-xs font-medium"
                        />
                        <label className="flex items-center gap-1 text-stone-600">
                          <input
                            type="checkbox"
                            checked={opt.required}
                            onChange={(e) => {
                              const next = [...(editing.options ?? [])];
                              next[idx] = { ...opt, required: e.target.checked };
                              setEditing({ ...editing, options: next });
                            }}
                          />
                          Required
                        </label>
                      </div>
                      <button
                        type="button"
                        onClick={() =>
                          setEditing({
                            ...editing,
                            options: editing.options?.filter((_, i) => i !== idx),
                          })
                        }
                        className="text-stone-400 hover:text-red-600"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Product Images (available once product has an ID) */}
            {editing.id ? (
              <div className="rounded-2xl border border-stone-200 p-4">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold uppercase tracking-wider text-stone-800">
                    Product Gallery Images ({editing.images?.length ?? 0})
                  </span>
                  <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-full bg-stone-900 px-4 py-1.5 text-xs font-semibold text-white hover:bg-amber-900">
                    <Upload className="h-3.5 w-3.5" /> Upload Image
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      onChange={(e) => void handleImageUpload(e.target.files)}
                      className="hidden"
                    />
                  </label>
                </div>

                <div className="mt-3 flex flex-wrap gap-3">
                  {editing.images?.map((img) => (
                    <div
                      key={img.id}
                      className="relative w-28 overflow-hidden rounded-xl border border-stone-200 bg-stone-50 p-1.5 text-center text-[11px]"
                    >
                      <img
                        src={img.url}
                        alt={img.alt}
                        className="aspect-square w-full rounded-lg object-cover"
                      />
                      <div className="mt-1.5 flex items-center justify-between px-1">
                        <button
                          type="button"
                          onClick={() => void handleSetPrimary(img.id)}
                          title="Set as primary image"
                          className={
                            img.isPrimary ? 'text-amber-600 font-bold' : 'text-stone-400 hover:text-amber-700'
                          }
                        >
                          <Star className="h-3.5 w-3.5 fill-current" />
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleRemoveImage(img.id)}
                          title="Remove image"
                          className="text-stone-400 hover:text-red-600"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-xs text-stone-500">
                Save the product first to unlock multi-image uploads.
              </p>
            )}

            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setEditing(null)}
                className="rounded-full border border-stone-300 px-5 py-2.5 text-xs font-semibold text-stone-700"
              >
                Close
              </button>
              <button
                type="submit"
                disabled={isSaving}
                className="rounded-full bg-emerald-700 px-6 py-2.5 text-xs font-semibold text-white hover:bg-emerald-800 disabled:opacity-50"
              >
                {isSaving ? 'Saving…' : editing.id ? 'Save Changes' : 'Create Product'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Products Table */}
      <div className="overflow-hidden rounded-3xl border border-stone-200 bg-white shadow-xs">
        {isLoading ? (
          <div className="p-8 text-center text-sm text-stone-500">Loading products…</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-stone-200 bg-stone-50 text-[11px] uppercase tracking-wider text-stone-500">
                <tr>
                  <th className="px-4 py-3.5">Product</th>
                  <th className="px-4 py-3.5">Type &amp; Status</th>
                  <th className="px-4 py-3.5">🇮🇳 GJ-IN</th>
                  <th className="px-4 py-3.5">🇺🇸 USA</th>
                  <th className="px-4 py-3.5">🇨🇦 Canada</th>
                  <th className="px-4 py-3.5">Options</th>
                  <th className="px-4 py-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stone-200">
                {products?.map((p) => {
                  const primary = p.images.find((i) => i.isPrimary) ?? p.images[0];
                  const inr = p.basePrices['GJ-IN']?.amount ?? 0;
                  const usd = p.basePrices['US']?.amount ?? 0;
                  const cad = p.basePrices['CA']?.amount ?? 0;
                  return (
                    <tr key={p.id} className="hover:bg-stone-50/80">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          {primary && (
                            <img
                              src={primary.url}
                              alt={primary.alt}
                              className="h-11 w-11 rounded-xl object-cover"
                            />
                          )}
                          <div>
                            <div className="font-bold text-stone-900">{p.name}</div>
                            <div className="text-[11px] text-stone-500">
                              /{p.slug} · {p.category}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-block rounded-full px-2.5 py-0.5 text-[11px] font-semibold capitalize ${
                            p.status === 'published'
                              ? 'bg-emerald-100 text-emerald-900'
                              : p.status === 'draft'
                              ? 'bg-amber-100 text-amber-900'
                              : 'bg-stone-200 text-stone-700'
                          }`}
                        >
                          {p.status}
                        </span>
                        <span className="ml-1.5 text-[11px] text-stone-500 capitalize">
                          ({p.type})
                        </span>
                      </td>
                      <td className="px-4 py-3 font-semibold text-stone-900">
                        {formatMoney(inr, 'INR', 'en-IN')}
                      </td>
                      <td className="px-4 py-3 font-semibold text-stone-900">
                        {formatMoney(usd, 'USD', 'en-US')}
                      </td>
                      <td className="px-4 py-3 font-semibold text-stone-900">
                        {formatMoney(cad, 'CAD', 'en-CA')}
                      </td>
                      <td className="px-4 py-3 text-stone-600">{p.options.length} options</td>
                      <td className="px-4 py-3 text-right">
                        <div className="inline-flex items-center gap-1.5">
                          <button
                            type="button"
                            onClick={() => setEditing(p)}
                            className="inline-flex items-center gap-1 rounded-full border border-stone-300 px-3 py-1 font-semibold text-stone-800 hover:border-amber-800"
                          >
                            <Edit3 className="h-3 w-3" /> Edit
                          </button>
                          {p.status !== 'published' ? (
                            <button
                              type="button"
                              onClick={() => void handleQuickStatus(p, 'published')}
                              title="Publish"
                              className="rounded-full border border-emerald-300 p-1.5 text-emerald-700 hover:bg-emerald-50"
                            >
                              <CheckCircle2 className="h-3.5 w-3.5" />
                            </button>
                          ) : (
                            <button
                              type="button"
                              onClick={() => void handleQuickStatus(p, 'archived')}
                              title="Archive"
                              className="rounded-full border border-stone-300 p-1.5 text-stone-600 hover:bg-stone-100"
                            >
                              <Archive className="h-3.5 w-3.5" />
                            </button>
                          )}
                          <button
                            type="button"
                            onClick={() => void handleDelete(p, true)}
                            title="Delete permanently"
                            className="rounded-full border border-red-200 p-1.5 text-red-600 hover:bg-red-50"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
