import React, { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  Clock,
  ExternalLink,
  MessageCircle,
  ShieldCheck,
  Sparkles,
  Trash2,
  Truck,
  Upload,
} from 'lucide-react';
import { api } from '../../api/client';
import type {
  LeadSubmissionResponse,
  OptionSelectionInput,
  ProductOptionDto,
  RegionalModifierDto,
  TempUploadResult,
} from '../../api/types';
import { formatMoney } from '../../lib/money';
import { useRegion } from '../region/RegionContext';

function renderInlineFormatting(text: string): React.ReactNode[] {
  const cleaned = text.replace(/`([^`]+)`/g, '$1');
  const tokens = cleaned.split(/(\*\*[^*]+\*\*|\*[^*]+\*)/g);
  return tokens.map((token, idx) => {
    if (token.startsWith('**') && token.endsWith('**') && token.length > 4) {
      return (
        <strong key={idx} className="font-bold text-[#161118]">
          {token.slice(2, -2)}
        </strong>
      );
    }
    if (token.startsWith('*') && token.endsWith('*') && token.length > 2) {
      return <em key={idx}>{token.slice(1, -1)}</em>;
    }
    return token;
  });
}

function renderFormattedDescription(description: string): React.ReactNode {
  const lines = description
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(
      (l) =>
        !/original instagram post/i.test(l) &&
        !/instagram\.com\/p\//i.test(l)
    );

  const blocks: React.ReactNode[] = [];
  let currentBullets: string[] = [];

  const flushBullets = (keyPrefix: string) => {
    if (currentBullets.length === 0) return;
    blocks.push(
      <ul key={keyPrefix} className="mt-2.5 list-disc space-y-1.5 pl-5">
        {currentBullets.map((item, i) => (
          <li key={i}>{renderInlineFormatting(item)}</li>
        ))}
      </ul>
    );
    currentBullets = [];
  };

  lines.forEach((line, idx) => {
    if (!line) {
      flushBullets(`ul-${idx}`);
      return;
    }
    if (line.startsWith('- ') || line.startsWith('• ')) {
      currentBullets.push(line.slice(2).trim());
    } else {
      flushBullets(`ul-${idx}`);
      blocks.push(
        <p key={`p-${idx}`} className={blocks.length > 0 ? 'mt-2.5' : ''}>
          {renderInlineFormatting(line)}
        </p>
      );
    }
  });

  flushBullets('ul-end');
  return blocks;
}

export const ProductDetail: React.FC = () => {
  const { slug = '' } = useParams<{ slug: string }>();
  const { region, regionDef, bootstrap, setCustomWhatsAppUrl } = useRegion();

  const {
    data: product,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['product', slug, region],
    queryFn: () => api.getProductBySlug(slug, region),
    enabled: Boolean(slug),
  });

  const [activeImageIdx, setActiveImageIdx] = useState(0);
  const [selections, setSelections] = useState<Record<string, OptionSelectionInput>>({});
  const [selectedExtras, setSelectedExtras] = useState<string[]>([]);
  const [uploadedPhotos, setUploadedPhotos] = useState<TempUploadResult[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  // Inline custom order / WhatsApp enquiry drawer state
  const [showRequestForm, setShowRequestForm] = useState(false);
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [notes, setNotes] = useState('');
  const [consentGiven, setConsentGiven] = useState(true);
  const [website, setWebsite] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState<LeadSubmissionResponse | null>(null);

  useEffect(() => {
    if (!product) return;
    const siteTitle = bootstrap?.business.name ?? 'Resin Delights by Dhara';
    document.title = `${product.name} | ${siteTitle}`;
    setActiveImageIdx(0);
    const initial: Record<string, OptionSelectionInput> = {};
    for (const opt of product.options) {
      if (opt.type === 'select' && opt.choices.length > 0) {
        initial[opt.id] = { optionId: opt.id, choiceId: opt.choices[0].id };
      }
    }
    setSelections(initial);
    setSelectedExtras([]);
    setUploadedPhotos([]);
    setShowRequestForm(product.type === 'custom');
    setSubmitted(null);
    setSubmitError(null);
  }, [product?.id]);

  const selectionList = useMemo(() => Object.values(selections), [selections]);

  const { data: liveQuote, isFetching: isQuoting } = useQuery({
    queryKey: ['quote', product?.id, region, selectionList, selectedExtras],
    queryFn: () =>
      api.calculateQuote({
        productId: product!.id,
        region,
        selections: selectionList,
        extras: selectedExtras,
      }),
    enabled: Boolean(product),
  });

  const quote = liveQuote ?? product?.initialQuote;

  const formatModBadge = (mod?: RegionalModifierDto | null) => {
    if (!mod) return null;
    if (mod.kind === 'fixed' && mod.amount && mod.amount > 0 && mod.currency) {
      return `+${formatMoney(mod.amount, mod.currency, regionDef.locale)}`;
    }
    if (mod.kind === 'percent' && mod.percent && mod.percent > 0) {
      return `+${mod.percent}%`;
    }
    return null;
  };

  const toggleExtra = (id: string) => {
    setSelectedExtras((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handlePhotoUpload = async (optId: string, fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    setUploadError(null);
    setIsUploading(true);
    try {
      const file = fileList[0];
      const saved = await api.uploadTempImage(file);
      setUploadedPhotos((prev) => [...prev, saved]);
      setSelections((prev) => ({
        ...prev,
        [optId]: { optionId: optId, provided: true, value: saved.tempId },
      }));
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Photo upload failed.');
    } finally {
      setIsUploading(false);
    }
  };

  const removeUploadedPhoto = (tempId: string) => {
    setUploadedPhotos((prev) => {
      const remaining = prev.filter((p) => p.tempId !== tempId);
      if (remaining.length === 0 && product) {
        setSelections((cur) => {
          const copy = { ...cur };
          for (const opt of product.options) {
            if (opt.type === 'image') {
              copy[opt.id] = { optionId: opt.id, provided: false, value: null };
            }
          }
          return copy;
        });
      }
      return remaining;
    });
  };

  const handleCustomSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!product) return;
    setSubmitError(null);
    setIsSubmitting(true);

    try {
      const result = await api.submitCustomRequest({
        productId: product.id,
        selectedRegion: region,
        selections: selectionList,
        extras: selectedExtras,
        tempImageIds: uploadedPhotos.map((p) => p.tempId),
        notes: notes || null,
        contact: {
          name,
          phone,
          email,
          address: address || null,
        },
        consentGiven,
        website,
      });

      setSubmitted(result);
      setCustomWhatsAppUrl(result.whatsappUrl);
      window.location.assign(result.whatsappUrl);
    } catch (err) {
      setSubmitError(
        err instanceof Error ? err.message : 'Could not submit your custom request.'
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
        <div className="grid gap-10 lg:grid-cols-2">
          <div className="aspect-square animate-pulse rounded-3xl bg-stone-200" />
          <div className="space-y-4">
            <div className="h-8 w-2/3 animate-pulse rounded bg-stone-200" />
            <div className="h-24 animate-pulse rounded bg-stone-200" />
            <div className="h-64 animate-pulse rounded-2xl bg-stone-200" />
          </div>
        </div>
      </div>
    );
  }

  if (error || !product) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-16 text-center">
        <h2 className="text-3xl font-bold text-stone-900">Product not found</h2>
        <p className="mt-2 text-sm text-stone-600">
          This piece may have been archived or moved. Browse our gallery to see available pieces.
        </p>
        <Link
          to="/products"
          className="mt-6 inline-flex items-center gap-2 rounded-full bg-stone-900 px-5 py-2.5 text-xs font-semibold text-white"
        >
          <ArrowLeft className="h-4 w-4" /> Back to Gallery
        </Link>
      </div>
    );
  }

  const activeImage = product.images[activeImageIdx] ?? product.images[0];
  const requireAddress = bootstrap?.leads.requireAddress ?? false;

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-6 flex items-center justify-between text-xs text-[#6E5A68]">
        <Link
          to="/products"
          className="inline-flex items-center gap-1.5 font-medium text-[#4A3E47] hover:text-[#5E3758]"
        >
          <ArrowLeft className="h-3.5 w-3.5" /> Back to collection
        </Link>
      </div>

      <div className="grid gap-10 lg:grid-cols-12">
        {/* Left: Image carousel */}
        <div className="lg:col-span-6">
          <div className="overflow-hidden rounded-3xl border border-[#EADCE0] bg-white shadow-luxe">
            {activeImage && (
              <img
                src={activeImage.url}
                alt={activeImage.alt}
                className="aspect-square w-full object-cover"
              />
            )}
          </div>
          {product.images.length > 1 && (
            <div className="mt-3 flex gap-3 overflow-x-auto pb-1">
              {product.images.map((img, idx) => (
                <button
                  key={img.id}
                  type="button"
                  onClick={() => setActiveImageIdx(idx)}
                  className={`relative h-20 w-20 shrink-0 overflow-hidden rounded-xl border-2 transition ${
                    idx === activeImageIdx
                      ? 'border-[#C99B3B] shadow-2xs'
                      : 'border-transparent opacity-70 hover:opacity-100'
                  }`}
                >
                  <img src={img.thumbUrl || img.url} alt={img.alt} className="h-full w-full object-cover" />
                </button>
              ))}
            </div>
          )}

          {/* Description & Craft Notes */}
          <div className="mt-8 rounded-2xl border border-[#EADCE0] bg-white p-6 shadow-luxe">
            <h2 className="text-xl font-bold text-[#161118]">About This Piece</h2>
            <div className="mt-3 text-sm leading-relaxed text-[#4A3E47]">
              {renderFormattedDescription(product.description)}
            </div>
            <div className="mt-5 flex flex-wrap gap-2">
              {product.tags.map((t) => (
                <span
                  key={t}
                  className="rounded-full border border-[#EADCE0] bg-[#F6EEF0] px-3 py-1 text-xs font-medium text-[#5E3758]"
                >
                  #{t}
                </span>
              ))}
            </div>
          </div>
        </div>

        {/* Right: Customizer & Regional Quote */}
        <div className="lg:col-span-6">
          <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe sm:p-8">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full border border-[#D98A9E]/50 bg-[#F6EEF0] px-3 py-1 text-xs font-semibold text-[#5E3758] uppercase">
                {product.category.replace('-', ' ')}
              </span>
              {product.type === 'custom' && (
                <span className="inline-flex items-center gap-1 rounded-full border border-[#C99B3B]/50 bg-obsidian-luxe px-3 py-1 text-xs font-semibold text-[#F3E5AB]">
                  <Sparkles className="h-3 w-3 text-[#C99B3B]" /> Custom Made for You
                </span>
              )}
              <span className="inline-flex items-center gap-1 text-xs text-[#6E5A68]">
                <Clock className="h-3.5 w-3.5 text-[#C86D8A]" /> Ships in about {product.leadTimeDays} days
              </span>
            </div>

            <h1 className="mt-3 text-3xl font-bold text-[#161118] sm:text-4xl">{product.name}</h1>
            <p className="mt-2 text-sm text-[#4A3E47]">{product.shortDescription}</p>

            {/* Live Regional Price Header */}
            {quote && (
              <div className="mt-6 rounded-2xl border border-[#C99B3B]/45 bg-gradient-to-r from-[#F6EEF0] via-[#FAF7F4] to-[#FBF4E6] p-4">
                <div className="flex items-baseline justify-between">
                  <div>
                    <span className="text-xs font-semibold uppercase tracking-wider text-[#5E3758]">
                      Estimated Total
                    </span>
                    <div className="mt-0.5 flex items-baseline gap-2">
                      <span className="tabular-price text-3xl font-bold text-[#161118]">
                        {formatMoney(quote.total, quote.currency, regionDef.locale)}
                      </span>
                      {isQuoting && (
                        <span className="text-xs font-medium text-[#9A7020]">Updating…</span>
                      )}
                    </div>
                  </div>
                  <span className="text-xs text-[#6E5A68]">{quote.taxLabel}</span>
                </div>
                {quote.shippingNote && (
                  <p className="mt-2 flex items-center gap-1.5 text-xs text-[#4A3E47]">
                    <Truck className="h-3.5 w-3.5 text-[#9A7020] shrink-0" />
                    {quote.shippingNote}
                  </p>
                )}
              </div>
            )}

            {/* Customization Options */}
            {product.options.length > 0 && (
              <div className="mt-6 space-y-5">
                <h2 className="text-lg font-bold text-stone-900">1. Choose Your Custom Options</h2>
                {product.options.map((opt: ProductOptionDto) => (
                  <div key={opt.id} className="space-y-2">
                    <div className="flex items-center justify-between">
                      <label className="text-xs font-semibold uppercase tracking-wider text-stone-700">
                        {opt.label}{' '}
                        {opt.required && <span className="text-amber-800">*</span>}
                      </label>
                      {opt.modifier && (
                        <span className="text-xs font-semibold text-amber-900">
                          {formatModBadge(opt.modifier)}
                        </span>
                      )}
                    </div>

                    {opt.type === 'select' && (
                      <div className="grid gap-2 sm:grid-cols-2">
                        {opt.choices.map((ch) => {
                          const selected = selections[opt.id]?.choiceId === ch.id;
                          const badge = formatModBadge(ch.modifier);
                          return (
                            <button
                              key={ch.id}
                              type="button"
                              onClick={() =>
                                setSelections((prev) => ({
                                  ...prev,
                                  [opt.id]: { optionId: opt.id, choiceId: ch.id },
                                }))
                              }
                              className={`flex items-center justify-between rounded-xl border px-3.5 py-2.5 text-left text-xs font-medium transition ${
                                selected
                                  ? 'border-amber-800 bg-amber-50/70 text-stone-900 ring-1 ring-amber-800'
                                  : 'border-stone-200 bg-white text-stone-700 hover:border-stone-300'
                              }`}
                            >
                              <span>{ch.label}</span>
                              {badge && (
                                <span className="ml-2 shrink-0 font-semibold text-amber-900">
                                  {badge}
                                </span>
                              )}
                            </button>
                          );
                        })}
                      </div>
                    )}

                    {opt.type === 'text' && (
                      <div>
                        <input
                          type="text"
                          maxLength={opt.constraints?.maxLength ?? 60}
                          placeholder={opt.constraints?.placeholder ?? 'Enter custom text…'}
                          value={selections[opt.id]?.value ?? ''}
                          onChange={(e) =>
                            setSelections((prev) => ({
                              ...prev,
                              [opt.id]: { optionId: opt.id, value: e.target.value },
                            }))
                          }
                          className="w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                        />
                        {opt.helpText && (
                          <p className="mt-1 text-xs text-stone-500">{opt.helpText}</p>
                        )}
                      </div>
                    )}

                    {opt.type === 'image' && (
                      <div className="space-y-2 rounded-xl border border-dashed border-stone-300 bg-stone-50/70 p-3.5">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div>
                            <span className="block text-xs font-semibold text-stone-900">
                              Upload your photo or design idea (kept private &amp; secure)
                            </span>
                            {opt.helpText && (
                              <span className="block text-[11px] text-stone-500">
                                {opt.helpText}
                              </span>
                            )}
                          </div>
                          <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-full bg-stone-900 px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-amber-900">
                            <Upload className="h-3.5 w-3.5" />
                            <span>{isUploading ? 'Uploading…' : 'Choose Photo'}</span>
                            <input
                              type="file"
                              accept="image/jpeg,image/png,image/webp"
                              disabled={isUploading}
                              onChange={(e) => void handlePhotoUpload(opt.id, e.target.files)}
                              className="hidden"
                            />
                          </label>
                        </div>

                        {uploadError && (
                          <p className="text-xs font-medium text-red-600">{uploadError}</p>
                        )}

                        {uploadedPhotos.length > 0 && (
                          <div className="flex flex-wrap gap-2 pt-1">
                            {uploadedPhotos.map((p) => (
                              <div
                                key={p.tempId}
                                className="flex items-center gap-2 rounded-lg border border-stone-200 bg-white p-1.5 pr-2.5 text-xs"
                              >
                                <img
                                  src={p.previewUrl}
                                  alt="Uploaded reference"
                                  className="h-9 w-9 rounded object-cover"
                                />
                                <span className="text-stone-700">
                                  {Math.round(p.sizeBytes / 1024)} KB
                                </span>
                                <button
                                  type="button"
                                  onClick={() => removeUploadedPhoto(p.tempId)}
                                  className="text-stone-400 hover:text-red-600"
                                  title="Remove photo"
                                >
                                  <Trash2 className="h-3.5 w-3.5" />
                                </button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    {opt.type === 'boolean' && (
                      <label className="flex cursor-pointer items-center justify-between rounded-xl border border-stone-200 px-3.5 py-2.5 text-xs">
                        <span className="font-medium text-stone-800">Add {opt.label}</span>
                        <input
                          type="checkbox"
                          checked={Boolean(selections[opt.id]?.provided)}
                          onChange={(e) =>
                            setSelections((prev) => ({
                              ...prev,
                              [opt.id]: { optionId: opt.id, provided: e.target.checked },
                            }))
                          }
                          className="h-4 w-4 accent-amber-800"
                        />
                      </label>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* Global Extras */}
            {product.extras.length > 0 && (
              <div className="mt-6 space-y-2 border-t border-stone-100 pt-5">
                <span className="text-xs font-semibold uppercase tracking-wider text-stone-700">
                  Optional Add-Ons &amp; Express Delivery
                </span>
                {product.extras.map((ex) => {
                  const checked = selectedExtras.includes(ex.id);
                  const badge = formatModBadge(ex.modifier);
                  return (
                    <label
                      key={ex.id}
                      className="flex cursor-pointer items-center justify-between rounded-xl border border-stone-200 px-3.5 py-2.5 text-xs hover:border-stone-300"
                    >
                      <span className="font-medium text-stone-800">
                        {ex.label}{' '}
                        {badge && <span className="font-semibold text-amber-900">({badge})</span>}
                      </span>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleExtra(ex.id)}
                        className="h-4 w-4 accent-amber-800"
                      />
                    </label>
                  );
                })}
              </div>
            )}

            {/* Server Quote Breakdown */}
            {quote && quote.lines.length > 0 && (
              <div className="mt-6 rounded-2xl bg-stone-50 p-4 text-xs">
                <div className="font-semibold text-stone-700">Price Breakdown</div>
                <dl className="mt-2 space-y-1.5">
                  {quote.lines.map((line, i) => (
                    <div key={i} className="flex justify-between text-stone-600">
                      <dt>{line.label}</dt>
                      <dd className="font-medium text-stone-900">
                        {line.amount >= 0 ? '' : '−'}
                        {formatMoney(Math.abs(line.amount), quote.currency, regionDef.locale)}
                      </dd>
                    </div>
                  ))}
                  <div className="flex justify-between border-t border-stone-200 pt-2 text-sm font-bold text-stone-900">
                    <dt>Estimated Total</dt>
                    <dd>{formatMoney(quote.total, quote.currency, regionDef.locale)}</dd>
                  </div>
                </dl>
              </div>
            )}

            {/* Step 2: Request / Order on WhatsApp */}
            {submitted ? (
              <div className="mt-6 rounded-2xl border border-emerald-200 bg-emerald-50/70 p-5 text-center">
                <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-700" />
                <h3 className="mt-2 text-xl font-bold text-stone-900">
                  Custom Request Saved ({submitted.reference})
                </h3>
                <p className="mt-1 text-xs text-stone-600">
                  Your customization and price quote have been logged for Dhara. Click below if
                  WhatsApp did not open automatically:
                </p>
                <a
                  href={submitted.whatsappUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-4 inline-flex items-center gap-2 rounded-full bg-emerald-700 px-6 py-3 text-xs font-semibold text-white hover:bg-emerald-800"
                >
                  <MessageCircle className="h-4 w-4" /> Continue to WhatsApp with My Order Details
                  <ExternalLink className="h-3.5 w-3.5" />
                </a>
              </div>
            ) : !showRequestForm ? (
              <div className="mt-6 flex flex-col gap-3 sm:flex-row">
                <button
                  type="button"
                  onClick={() => setShowRequestForm(true)}
                  className="inline-flex flex-1 items-center justify-center gap-2 rounded-full bg-emerald-700 px-6 py-3.5 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-800"
                >
                  <MessageCircle className="h-4 w-4" />
                  Order on WhatsApp
                </button>
                <Link
                  to={`/contact?product=${encodeURIComponent(product.slug)}`}
                  className="inline-flex items-center justify-center gap-2 rounded-full border border-stone-300 bg-white px-5 py-3.5 text-xs font-semibold text-stone-800 hover:border-amber-800"
                >
                  Ask a Question
                </Link>
              </div>
            ) : (
              <form
                onSubmit={handleCustomSubmit}
                className="mt-6 space-y-3.5 rounded-2xl border border-amber-200 bg-amber-50/40 p-5"
                noValidate
              >
                <div className="flex items-center justify-between">
                  <h3 className="text-base font-bold text-stone-900">
                    2. Your Details for WhatsApp Confirmation
                  </h3>
                  {product.type !== 'custom' && (
                    <button
                      type="button"
                      onClick={() => setShowRequestForm(false)}
                      className="text-xs text-stone-500 hover:text-stone-800"
                    >
                      Hide
                    </button>
                  )}
                </div>

                {submitError && (
                  <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-800">
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
                    <span>{submitError}</span>
                  </div>
                )}

                <div className="hidden" aria-hidden="true">
                  <input
                    type="text"
                    tabIndex={-1}
                    autoComplete="off"
                    value={website}
                    onChange={(e) => setWebsite(e.target.value)}
                  />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="block text-[11px] font-semibold uppercase text-stone-700">
                      Your Name *
                    </label>
                    <input
                      type="text"
                      required
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="Priya Shah"
                      className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-xs text-stone-900"
                    />
                  </div>
                  <div>
                    <label className="block text-[11px] font-semibold uppercase text-stone-700">
                      WhatsApp / Phone *
                    </label>
                    <input
                      type="tel"
                      required
                      value={phone}
                      onChange={(e) => setPhone(e.target.value)}
                      placeholder={region === 'GJ-IN' ? '+91 98765 43210' : '+1 415 555 0199'}
                      className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-xs text-stone-900"
                    />
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="block text-[11px] font-semibold uppercase text-stone-700">
                      Email *
                    </label>
                    <input
                      type="email"
                      required
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="priya@example.com"
                      className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-xs text-stone-900"
                    />
                  </div>
                  <div>
                    <label className="block text-[11px] font-semibold uppercase text-stone-700">
                      City / Delivery Address {requireAddress ? '*' : '(optional)'}
                    </label>
                    <input
                      type="text"
                      required={requireAddress}
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="Ahmedabad, Gujarat / Toronto, ON"
                      className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-xs text-stone-900"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-[11px] font-semibold uppercase text-stone-700">
                    Special Instructions / Event Date (optional)
                  </label>
                  <input
                    type="text"
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    placeholder="e.g. Need before 15 Dec for wedding anniversary"
                    className="mt-1 w-full rounded-xl border border-stone-300 bg-white px-3 py-2 text-xs text-stone-900"
                  />
                </div>

                <label className="flex items-start gap-2 text-[11px] text-stone-600">
                  <input
                    type="checkbox"
                    checked={consentGiven}
                    onChange={(e) => setConsentGiven(e.target.checked)}
                    className="mt-0.5 h-3.5 w-3.5 accent-amber-800"
                  />
                  <span>
                    Save my selected options &amp; contact details so Dhara can assist me on
                    WhatsApp (<Link to="/privacy" className="underline">Privacy Notice</Link>).
                  </span>
                </label>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="inline-flex w-full items-center justify-center gap-2 rounded-full bg-emerald-700 px-6 py-3 text-xs font-semibold text-white shadow-sm hover:bg-emerald-800 disabled:opacity-60"
                >
                  <MessageCircle className="h-4 w-4" />
                  {isSubmitting
                    ? 'Saving Quote & Opening WhatsApp…'
                    : 'Submit Custom Request & Open WhatsApp'}
                </button>
              </form>
            )}

            <div className="mt-5 grid grid-cols-2 gap-3 border-t border-stone-100 pt-4 text-[11px] text-stone-500">
              <div className="flex items-center gap-1.5">
                <CheckCircle2 className="h-4 w-4 text-amber-800 shrink-0" />
                Handmade in Gujarat, India
              </div>
              <div className="flex items-center gap-1.5">
                <ShieldCheck className="h-4 w-4 text-amber-800 shrink-0" />
                Final price confirmed on WhatsApp
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
