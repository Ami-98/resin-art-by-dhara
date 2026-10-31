import React, { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Archive, Check, Download, ExternalLink, Save } from 'lucide-react';
import { api } from '../../api/client';
import type { EditableSettingsPayload } from '../../api/types';

export const AdminSettingsPage: React.FC = () => {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'settings'],
    queryFn: api.getAdminSettings,
  });

  const { data: backups } = useQuery({
    queryKey: ['admin', 'backups'],
    queryFn: api.getAdminBackups,
  });

  const [form, setForm] = useState<EditableSettingsPayload | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isRunningBackup, setIsRunningBackup] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (data) setForm(structuredClone(data));
  }, [data]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setIsSaving(true);
    setStatusMessage(null);
    setError(null);

    try {
      const res = await api.updateAdminSettings({
        business: form.business,
        regions: form.regions,
        leads: form.leads,
        features: form.features,
        seo: form.seo,
        uploads: form.uploads,
        catalog: form.catalog,
        maintenance: form.maintenance,
      });
      setForm(res.settings);
      setStatusMessage(
        `Saved to appsettings.Business.json & hot-reloaded${
          res.backupFileName ? ` (backup: ${res.backupFileName})` : ''
        }.`
      );
      await queryClient.invalidateQueries({ queryKey: ['bootstrap'] });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update settings.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleRunBackup = async () => {
    setIsRunningBackup(true);
    setStatusMessage(null);
    setError(null);
    try {
      const report = await api.runAdminMaintenance();
      setStatusMessage(
        `Created ZIP snapshot ${report.backupCreated?.fileName ?? ''} · Cleaned ${
          report.orphanedTempFilesDeleted
        } temp files & ${
          report.expiredContactsDeleted + report.expiredRequestsDeleted
        } expired leads.`
      );
      await queryClient.invalidateQueries({ queryKey: ['admin', 'backups'] });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Backup job failed.');
    } finally {
      setIsRunningBackup(false);
    }
  };

  if (isLoading || !form) {
    return <div className="p-8 text-sm text-stone-500">Loading configuration…</div>;
  }

  const previewWhatsAppUrl = `${form.business.whatsApp.baseUrl.replace(/\/$/, '')}/${
    form.business.whatsApp.number
  }?text=${encodeURIComponent(
    form.business.whatsApp.includeReference
      ? `${form.business.whatsApp.defaultMessage} (Ref RA-C-2026-0101)`
      : form.business.whatsApp.defaultMessage
  )}`;

  return (
    <div className="space-y-8">
      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-3xl font-bold text-stone-900">
              Application Configuration (appsettings.json)
            </h1>
            <p className="mt-1 text-xs text-stone-600">
              Persisted in <code>{form.configFilePath}</code> and layered over{' '}
              <code>appsettings.json</code> with instant hot-reload via{' '}
              <code>IOptionsMonitor</code>.
            </p>
          </div>

          <button
            type="submit"
            disabled={isSaving}
            className="inline-flex items-center gap-2 rounded-full bg-emerald-700 px-6 py-2.5 text-xs font-semibold text-white hover:bg-emerald-800 disabled:opacity-50"
          >
            <Save className="h-4 w-4" />
            {isSaving ? 'Saving & Reloading…' : 'Save & Hot-Reload Settings'}
          </button>
        </div>

        {statusMessage && (
          <div className="flex items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-semibold text-emerald-900">
            <Check className="h-4 w-4 text-emerald-700" /> {statusMessage}
          </div>
        )}

        {error && (
          <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-xs font-semibold text-red-800">
            {error}
          </div>
        )}

        <div className="grid gap-6 lg:grid-cols-2">
          {/* WhatsApp & Studio Identity */}
          <div className="space-y-4 rounded-3xl border border-stone-200 bg-white p-6 shadow-xs">
            <h2 className="text-xl font-bold text-stone-900">
              Studio Identity &amp; WhatsApp Redirect
            </h2>

            <div>
              <label className="block text-xs font-semibold uppercase text-stone-700">
                Business Name
              </label>
              <input
                type="text"
                required
                value={form.business.name}
                onChange={(e) =>
                  setForm({
                    ...form,
                    business: { ...form.business, name: e.target.value },
                  })
                }
                className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
              />
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Studio Tagline
                </label>
                <input
                  type="text"
                  value={form.business.tagline ?? ''}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      business: { ...form.business, tagline: e.target.value },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Studio Contact Email
                </label>
                <input
                  type="email"
                  value={form.business.contactEmail ?? ''}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      business: { ...form.business, contactEmail: e.target.value },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  WhatsApp Number (international digits only)
                </label>
                <input
                  type="text"
                  required
                  value={form.business.whatsApp.number}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      business: {
                        ...form.business,
                        whatsApp: { ...form.business.whatsApp, number: e.target.value.trim() },
                      },
                    })
                  }
                  placeholder="919876543210"
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Pre-filled WhatsApp Message
                </label>
                <input
                  type="text"
                  required
                  value={form.business.whatsApp.defaultMessage}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      business: {
                        ...form.business,
                        whatsApp: { ...form.business.whatsApp, defaultMessage: e.target.value },
                      },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>
            </div>

            <label className="flex items-center gap-2 text-xs font-medium text-stone-700">
              <input
                type="checkbox"
                checked={form.business.whatsApp.includeReference}
                onChange={(e) =>
                  setForm({
                    ...form,
                    business: {
                      ...form.business,
                      whatsApp: {
                        ...form.business.whatsApp,
                        includeReference: e.target.checked,
                      },
                    },
                  })
                }
                className="h-4 w-4 accent-amber-800"
              />
              Append lead reference code to the &ldquo;Hi&rdquo; message
            </label>

            <div className="rounded-2xl bg-stone-50 p-3.5 text-xs">
              <div className="font-semibold text-stone-700">Live WhatsApp Redirect Preview:</div>
              <div className="mt-1 truncate font-mono text-stone-600">{previewWhatsAppUrl}</div>
              <a
                href={previewWhatsAppUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="mt-2 inline-flex items-center gap-1 font-semibold text-emerald-700 underline"
              >
                Test wa.me link <ExternalLink className="h-3 w-3" />
              </a>
            </div>
          </div>

          {/* Regional Defaults & Feature Toggles */}
          <div className="space-y-4 rounded-3xl border border-stone-200 bg-white p-6 shadow-xs">
            <h2 className="text-xl font-bold text-stone-900">
              Regional Defaults, Leads &amp; Features
            </h2>

            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Default Store Region
                </label>
                <select
                  value={form.regions.default}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      regions: { ...form.regions, default: e.target.value },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                >
                  {form.regions.definitions.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.flag} {r.name} ({r.currency})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Fallback for Other Countries
                </label>
                <select
                  value={form.regions.fallbackForUnsupported}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      regions: { ...form.regions, fallbackForUnsupported: e.target.value },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                >
                  {form.regions.definitions.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.flag} {r.name} ({r.currency})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="space-y-2 pt-2">
              <label className="flex items-center gap-2 text-xs font-medium text-stone-800">
                <input
                  type="checkbox"
                  checked={form.leads.requireAddress}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      leads: { ...form.leads, requireAddress: e.target.checked },
                    })
                  }
                  className="h-4 w-4 accent-amber-800"
                />
                Require street address on Contact &amp; Custom Request forms
              </label>

              <label className="flex items-center gap-2 text-xs font-medium text-stone-800">
                <input
                  type="checkbox"
                  checked={form.features.showRegionBanner}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      features: { ...form.features, showRegionBanner: e.target.checked },
                    })
                  }
                  className="h-4 w-4 accent-amber-800"
                />
                Show top regional pricing banner to visitors
              </label>

              <label className="flex items-center gap-2 text-xs font-medium text-stone-800">
                <input
                  type="checkbox"
                  checked={form.features.enableCustomRequests}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      features: { ...form.features, enableCustomRequests: e.target.checked },
                    })
                  }
                  className="h-4 w-4 accent-amber-800"
                />
                Enable custom product commissions
              </label>
            </div>

            <div className="grid gap-3 border-t border-stone-100 pt-3 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold uppercase text-stone-700">
                  Lead Retention (Months)
                </label>
                <input
                  type="number"
                  min={1}
                  max={120}
                  value={form.leads.retentionMonths}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      leads: { ...form.leads, retentionMonths: Number(e.target.value) },
                    })
                  }
                  className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                />
              </div>

              {form.uploads && (
                <div>
                  <label className="block text-xs font-semibold uppercase text-stone-700">
                    Max Upload Size (MB)
                  </label>
                  <input
                    type="number"
                    min={1}
                    max={50}
                    value={form.uploads.maxFileSizeMb}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        uploads: {
                          ...form.uploads!,
                          maxFileSizeMb: Number(e.target.value),
                        },
                      })
                    }
                    className="mt-1 w-full rounded-xl border border-stone-300 px-3.5 py-2 text-sm"
                  />
                </div>
              )}
            </div>
          </div>
        </div>
      </form>

      {/* Automated Backups & Retention Maintenance */}
      <div className="rounded-3xl border border-stone-200 bg-white p-6 shadow-xs space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-stone-900">
              Data Root Snapshots &amp; Retention Maintenance
            </h2>
            <p className="text-xs text-stone-500">
              Creates a downloadable ZIP archive of all JSON collections (<code>db/*.json</code>)
              and <code>appsettings.Business.json</code>, and cleans up expired leads &amp;
              orphaned temporary uploads.
            </p>
          </div>
          <button
            type="button"
            onClick={() => void handleRunBackup()}
            disabled={isRunningBackup}
            className="inline-flex items-center gap-2 rounded-full bg-stone-900 px-5 py-2.5 text-xs font-semibold text-white hover:bg-amber-900 disabled:opacity-50"
          >
            <Archive className="h-4 w-4" />
            {isRunningBackup ? 'Running Snapshot…' : 'Create Snapshot Backup Now'}
          </button>
        </div>

        {backups && backups.length > 0 && (
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 pt-2">
            {backups.slice(0, 6).map((b) => (
              <a
                key={b.fileName}
                href={`/api/admin/backups/${encodeURIComponent(b.fileName)}`}
                className="flex items-center justify-between rounded-2xl border border-stone-200 bg-stone-50 px-4 py-2.5 text-xs hover:border-amber-800"
              >
                <div className="truncate pr-2">
                  <div className="truncate font-mono font-semibold text-stone-800">
                    {b.fileName}
                  </div>
                  <div className="text-[11px] text-stone-500">
                    {(b.sizeBytes / 1024).toFixed(1)} KB
                  </div>
                </div>
                <Download className="h-4 w-4 shrink-0 text-amber-800" />
              </a>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
