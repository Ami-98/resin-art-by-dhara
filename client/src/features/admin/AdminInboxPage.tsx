import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Download,
  MessageCircle,
  Trash2,
} from 'lucide-react';
import { api } from '../../api/client';
import type { LeadStatus } from '../../api/types';
import { formatMoney } from '../../lib/money';
import { useRegion } from '../region/RegionContext';

const STATUS_OPTIONS: LeadStatus[] = [
  'new',
  'contacted',
  'quoted',
  'confirmed',
  'inProgress',
  'delivered',
  'closed',
  'cancelled',
];

export const AdminInboxPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { bootstrap } = useRegion();
  const [tab, setTab] = useState<'requests' | 'contacts'>('requests');

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'leads'],
    queryFn: () => api.getAdminLeads(),
  });

  const handleStatusChange = async (
    kind: 'contacts' | 'requests',
    id: string,
    status: LeadStatus,
    adminNotes: string
  ) => {
    await api.updateAdminLead(kind, id, status, adminNotes);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'leads'] });
    await queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
  };

  const handleDelete = async (kind: 'contacts' | 'requests', id: string) => {
    await api.deleteAdminLead(kind, id);
    await queryClient.invalidateQueries({ queryKey: ['admin', 'leads'] });
    await queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
  };

  const buildCustomerWhatsAppUrl = (phone: string, name: string, ref: string) => {
    const digits = phone.replace(/\D/g, '');
    const template =
      bootstrap?.business.customerReplyTemplate ??
      'Hi {name}, this is {owner} from {business} regarding your enquiry ({reference}).';
    const text = template
      .replace('{name}', name)
      .replace('{owner}', bootstrap?.business.ownerName ?? 'Dhara')
      .replace('{business}', bootstrap?.business.name ?? 'Resin Art by Dhara')
      .replace('{reference}', ref);
    return `https://wa.me/${digits}?text=${encodeURIComponent(text)}`;
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-stone-900">Customer Leads &amp; Enquiries Inbox</h1>
          <p className="mt-1 text-sm text-stone-600">
            Every contact form submission and custom product request logged in JSON storage.
          </p>
        </div>

        <a
          href="/api/admin/leads/export.csv"
          className="inline-flex items-center gap-2 rounded-full border border-stone-300 bg-white px-4 py-2 text-xs font-semibold text-stone-800 hover:border-amber-800"
        >
          <Download className="h-3.5 w-3.5" /> Export All Leads (.CSV)
        </a>
      </div>

      {/* Tabs */}
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => setTab('requests')}
          className={`rounded-full px-5 py-2 text-xs font-semibold ${
            tab === 'requests'
              ? 'bg-stone-900 text-white'
              : 'border border-stone-300 bg-white text-stone-700'
          }`}
        >
          Custom Product Requests ({data?.customRequests.length ?? 0})
        </button>
        <button
          type="button"
          onClick={() => setTab('contacts')}
          className={`rounded-full px-5 py-2 text-xs font-semibold ${
            tab === 'contacts'
              ? 'bg-stone-900 text-white'
              : 'border border-stone-300 bg-white text-stone-700'
          }`}
        >
          Contact Form Submissions ({data?.contacts.length ?? 0})
        </button>
      </div>

      {isLoading ? (
        <div className="p-8 text-sm text-stone-500">Loading leads…</div>
      ) : tab === 'requests' ? (
        <div className="space-y-4">
          {data?.customRequests.length === 0 && (
            <div className="rounded-3xl border border-stone-200 bg-white p-10 text-center text-sm text-stone-500">
              No custom requests submitted yet.
            </div>
          )}
          {data?.customRequests.map((r) => (
            <div
              key={r.id}
              className="rounded-3xl border border-stone-200 bg-white p-6 shadow-xs space-y-4"
            >
              <div className="flex flex-wrap items-start justify-between gap-4 border-b border-stone-100 pb-4">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-full bg-amber-100 px-3 py-0.5 text-xs font-bold text-amber-950">
                      {r.reference}
                    </span>
                    <span className="font-bold text-base text-stone-900">
                      {r.productNameSnapshot}
                    </span>
                    <span className="text-xs text-stone-500">
                      · {new Date(r.createdAt).toLocaleString()}
                    </span>
                    {r.meta.regionMismatch && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-300 px-2.5 py-0.5 text-[11px] font-semibold text-amber-900">
                        <AlertTriangle className="h-3 w-3" /> Selected {r.selectedRegion} (IP:{' '}
                        {r.detectedRegion})
                      </span>
                    )}
                  </div>
                  <div className="mt-1 text-xs text-stone-600">
                    <strong>{r.contact.name}</strong> · {r.contact.phone} · {r.contact.email}
                    {r.contact.address ? ` · ${r.contact.address}` : ''}
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded-xl bg-stone-100 px-3 py-1.5 text-sm font-bold text-stone-900">
                    Quoted: {formatMoney(r.quote.total, r.quote.currency)} ({r.selectedRegion})
                  </span>
                  <select
                    value={r.status}
                    onChange={(e) =>
                      void handleStatusChange(
                        'requests',
                        r.id,
                        e.target.value as LeadStatus,
                        r.adminNotes
                      )
                    }
                    className="rounded-full border border-stone-300 bg-white px-3 py-1.5 text-xs font-semibold capitalize"
                  >
                    {STATUS_OPTIONS.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                  <a
                    href={buildCustomerWhatsAppUrl(r.contact.phone, r.contact.name, r.reference)}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1 rounded-full bg-emerald-700 px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-emerald-800"
                  >
                    <MessageCircle className="h-3.5 w-3.5" /> Reply on WhatsApp
                  </a>
                  <button
                    type="button"
                    onClick={() => void handleDelete('requests', r.id)}
                    title="Delete customer data"
                    className="rounded-full border border-red-200 p-1.5 text-red-600 hover:bg-red-50"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2 text-xs">
                <div className="rounded-2xl bg-stone-50 p-3.5 space-y-1.5">
                  <div className="font-semibold text-stone-800">Selected Customizations</div>
                  {r.selections.map((s) => (
                    <div key={s.optionId} className="text-stone-600">
                      <span className="font-medium text-stone-800">{s.optionLabel}:</span>{' '}
                      {s.choiceLabel || s.value || (s.provided ? 'Yes' : '—')}
                    </div>
                  ))}
                  {r.notes && (
                    <div className="pt-1 text-stone-700">
                      <strong>Customer Notes:</strong> {r.notes}
                    </div>
                  )}
                  {r.referenceImages.length > 0 && (
                    <div className="flex gap-2 pt-2">
                      {r.referenceImages.map((imgUrl) => (
                        <a
                          key={imgUrl}
                          href={imgUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                        >
                          <img
                            src={imgUrl}
                            alt="Customer reference"
                            className="h-14 w-14 rounded-lg border border-stone-300 object-cover"
                          />
                        </a>
                      ))}
                    </div>
                  )}
                </div>

                <div className="rounded-2xl bg-stone-50 p-3.5 space-y-1">
                  <div className="font-semibold text-stone-800">Quote Snapshot Breakdown</div>
                  {r.quote.lines.map((l, idx) => (
                    <div key={idx} className="flex justify-between text-stone-600">
                      <span>{l.label}</span>
                      <span className="font-medium text-stone-900">
                        {formatMoney(l.amount, r.quote.currency)}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="space-y-3">
          {data?.contacts.length === 0 && (
            <div className="rounded-3xl border border-stone-200 bg-white p-10 text-center text-sm text-stone-500">
              No contact enquiries logged yet.
            </div>
          )}
          {data?.contacts.map((c) => (
            <div
              key={c.id}
              className="flex flex-wrap items-center justify-between gap-4 rounded-3xl border border-stone-200 bg-white p-5 shadow-xs"
            >
              <div className="space-y-1 text-xs">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 font-bold text-emerald-900">
                    {c.reference}
                  </span>
                  <span className="text-sm font-bold text-stone-900">{c.name}</span>
                  <span className="text-stone-500">
                    · {c.phone} · {c.email}
                  </span>
                  <span className="rounded bg-stone-100 px-2 py-0.5 font-medium text-stone-700">
                    Region: {c.selectedRegion}
                  </span>
                  {c.meta.regionMismatch && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-300 px-2 py-0.5 text-[11px] font-semibold text-amber-900">
                      <AlertTriangle className="h-3 w-3" /> IP: {c.detectedRegion}
                    </span>
                  )}
                </div>
                {c.address && <div className="text-stone-600">Address: {c.address}</div>}
                {c.productSlug && (
                  <div className="text-amber-900 font-medium">
                    Product: {c.productName ?? c.productSlug}
                  </div>
                )}
                {c.message && <div className="text-stone-700">Message: &ldquo;{c.message}&rdquo;</div>}
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <select
                  value={c.status}
                  onChange={(e) =>
                    void handleStatusChange(
                      'contacts',
                      c.id,
                      e.target.value as LeadStatus,
                      c.adminNotes
                    )
                  }
                  className="rounded-full border border-stone-300 bg-white px-3 py-1.5 text-xs font-semibold capitalize"
                >
                  {STATUS_OPTIONS.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
                <a
                  href={buildCustomerWhatsAppUrl(c.phone, c.name, c.reference)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1 rounded-full bg-emerald-700 px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-emerald-800"
                >
                  <MessageCircle className="h-3.5 w-3.5" /> WhatsApp Customer
                </a>
                <button
                  type="button"
                  onClick={() => void handleDelete('contacts', c.id)}
                  className="rounded-full border border-red-200 p-1.5 text-red-600 hover:bg-red-50"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
