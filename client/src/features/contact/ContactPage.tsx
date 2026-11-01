import React, { useEffect, useState } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import {
  AlertCircle,
  CheckCircle2,
  ExternalLink,
  Mail,
  MapPin,
  MessageCircle,
  Phone,
  ShieldCheck,
  User,
} from 'lucide-react';
import { api } from '../../api/client';
import type { LeadSubmissionResponse } from '../../api/types';
import { useRegion } from '../region/RegionContext';

export const ContactPage: React.FC = () => {
  const { region, bootstrap, setCustomWhatsAppUrl, buildWhatsAppUrlWithText } = useRegion();
  const [params] = useSearchParams();
  const location = useLocation();
  const productSlug = params.get('product') ?? '';

  const requireAddress = bootstrap?.leads.requireAddress ?? false;

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [message, setMessage] = useState('');
  const [consentGiven, setConsentGiven] = useState(true);
  const [website, setWebsite] = useState(''); // honeypot

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState<LeadSubmissionResponse | null>(null);

  const buildDraftWhatsAppUrl = () => {
    const lines = ["Hi, I'm interested in Resin art"];
    const hasDetails = Boolean(
      name.trim() || phone.trim() || email.trim() || address.trim() || productSlug || message.trim()
    );
    if (hasDetails) {
      lines.push('');
      if (name.trim()) lines.push(`Name: ${name.trim()}`);
      if (phone.trim()) lines.push(`Phone: ${phone.trim()}`);
      if (email.trim()) lines.push(`Email: ${email.trim()}`);
      if (address.trim()) lines.push(`Address: ${address.trim()}`);
      if (region) lines.push(`Region: ${region}`);
      if (productSlug) lines.push(`Product: ${productSlug.replace(/-/g, ' ')}`);
      if (message.trim()) lines.push(`Notes: ${message.trim()}`);
    }
    return buildWhatsAppUrlWithText(lines.join('\n'));
  };

  useEffect(() => {
    if (submitted?.whatsappUrl) {
      setCustomWhatsAppUrl(submitted.whatsappUrl);
      return;
    }
    const hasDetails = Boolean(
      name.trim() || phone.trim() || email.trim() || address.trim() || productSlug || message.trim()
    );
    if (hasDetails) {
      setCustomWhatsAppUrl(buildDraftWhatsAppUrl());
    }
  }, [name, phone, email, address, productSlug, message, region, submitted]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await api.submitContact({
        name,
        phone,
        email,
        address: address || null,
        message: message || null,
        productSlug: productSlug || null,
        selectedRegion: region,
        sourcePage: location.pathname + location.search,
        consentGiven,
        website,
      });

      setSubmitted(result);
      setCustomWhatsAppUrl(result.whatsappUrl);
      // Redirect the client to WhatsApp with the pre-filled message including form details
      window.location.assign(result.whatsappUrl);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit your enquiry. Please try again.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6 lg:px-8">
      <div className="grid gap-8 lg:grid-cols-12">
        {/* Left studio info column */}
        <div className="lg:col-span-5">
          <div className="bg-obsidian-luxe rounded-3xl border border-[#C99B3B]/40 p-7 text-[#FAF7F4] shadow-luxe">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-[#F3E5AB]">
              Gujarat Studio · Chat with Us
            </span>
            <h1 className="mt-2 text-3xl font-bold text-white">
              Let&rsquo;s plan your custom resin piece
            </h1>
            <p className="mt-3 text-sm leading-relaxed text-[#E5D7DF]">
              Share your contact details below and we will open WhatsApp with your name, notes, and
              enquiry details pre-filled so Dhara knows who is messaging and can help you with
              colors, flower preservation steps, and delivery dates.
            </p>

            {productSlug && (
              <div className="mt-5 rounded-2xl border border-[#C99B3B]/40 bg-[#5E3758]/40 p-3.5 text-xs text-[#F4C2D7]">
                Asking about:{' '}
                <strong className="capitalize text-white">
                  {productSlug.replace(/-/g, ' ')}
                </strong>
              </div>
            )}

            <div className="mt-6 space-y-3 border-t border-[#C99B3B]/25 pt-5 text-xs text-[#D5C5CE]">
              <div className="flex items-center gap-2.5">
                <MapPin className="h-4 w-4 text-[#F3E5AB] shrink-0" />
                <span>Handmade in Gujarat, India · Safe shipping to India, USA &amp; Canada</span>
              </div>
              {bootstrap?.business.contactEmail && (
                <div className="flex items-center gap-2.5">
                  <Mail className="h-4 w-4 text-[#F3E5AB] shrink-0" />
                  <span>{bootstrap.business.contactEmail}</span>
                </div>
              )}
              <div className="flex items-center gap-2.5">
                <ShieldCheck className="h-4 w-4 text-[#F3E5AB] shrink-0" />
                <span>Your details are kept private and used only to help with your order.</span>
              </div>
            </div>
          </div>
        </div>

        {/* Right form column */}
        <div className="lg:col-span-7">
          <div className="rounded-3xl border border-[#EADCE0] bg-white p-6 shadow-luxe sm:p-8">
            {submitted ? (
              <div className="py-6 text-center">
                <CheckCircle2 className="mx-auto h-12 w-12 text-emerald-600" />
                <h2 className="mt-3 text-2xl font-bold text-stone-900">
                  Details saved · Opening WhatsApp…
                </h2>
                <p className="mt-1 text-sm text-stone-600">
                  Reference: <strong>{submitted.reference}</strong>. If WhatsApp did not open
                  automatically, click the button below:
                </p>
                <a
                  href={submitted.whatsappUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-6 inline-flex items-center gap-2 rounded-full bg-emerald-700 px-6 py-3.5 text-sm font-semibold text-white hover:bg-emerald-800"
                >
                  <MessageCircle className="h-4 w-4" /> Open WhatsApp with My Details
                  <ExternalLink className="h-3.5 w-3.5" />
                </a>
              </div>
            ) : (
              <form onSubmit={handleSubmit} className="space-y-4" noValidate>
                <div>
                  <h2 className="text-2xl font-bold text-stone-900">Contact &amp; WhatsApp Chat</h2>
                  <p className="mt-1 text-xs text-stone-500">
                    Share your details below to connect directly with our studio on WhatsApp.
                  </p>
                </div>

                {error && (
                  <div className="rounded-2xl border border-red-200 bg-red-50 p-3.5 text-xs text-red-800">
                    <div className="flex items-start gap-2">
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />
                      <div>
                        <p className="font-semibold">{error}</p>
                        {bootstrap?.business.whatsAppUrl && (
                          <p className="mt-1">
                            Having trouble?{' '}
                            <a
                              href={buildDraftWhatsAppUrl()}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="font-semibold underline"
                            >
                              Message us directly on WhatsApp →
                            </a>
                          </p>
                        )}
                      </div>
                    </div>
                  </div>
                )}

                {/* Honeypot field hidden from human visitors */}
                <div className="hidden" aria-hidden="true">
                  <label>
                    Website
                    <input
                      type="text"
                      tabIndex={-1}
                      autoComplete="off"
                      value={website}
                      onChange={(e) => setWebsite(e.target.value)}
                    />
                  </label>
                </div>

                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                    Full Name <span className="text-amber-800">*</span>
                  </label>
                  <div className="relative mt-1">
                    <User className="pointer-events-none absolute top-3 left-3.5 h-4 w-4 text-stone-400" />
                    <input
                      type="text"
                      required
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="Priya Shah"
                      className="w-full rounded-xl border border-stone-300 py-2.5 pr-3.5 pl-10 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                    />
                  </div>
                </div>

                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                      Phone / WhatsApp Number <span className="text-amber-800">*</span>
                    </label>
                    <div className="relative mt-1">
                      <Phone className="pointer-events-none absolute top-3 left-3.5 h-4 w-4 text-stone-400" />
                      <input
                        type="tel"
                        required
                        value={phone}
                        onChange={(e) => setPhone(e.target.value)}
                        placeholder={
                          region === 'GJ-IN' ? '+91 98765 43210' : '+1 415 555 0199'
                        }
                        className="w-full rounded-xl border border-stone-300 py-2.5 pr-3.5 pl-10 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                      Email Address <span className="text-amber-800">*</span>
                    </label>
                    <div className="relative mt-1">
                      <Mail className="pointer-events-none absolute top-3 left-3.5 h-4 w-4 text-stone-400" />
                      <input
                        type="email"
                        required
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        placeholder="priya@example.com"
                        className="w-full rounded-xl border border-stone-300 py-2.5 pr-3.5 pl-10 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                    Address / City &amp; Country{' '}
                    {requireAddress ? (
                      <span className="text-amber-800">*</span>
                    ) : (
                      <span className="font-normal text-stone-400">(optional for initial chat)</span>
                    )}
                  </label>
                  <div className="relative mt-1">
                    <MapPin className="pointer-events-none absolute top-3 left-3.5 h-4 w-4 text-stone-400" />
                    <input
                      type="text"
                      required={requireAddress}
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="e.g. Navrangpura, Ahmedabad, Gujarat / Edison, NJ, USA"
                      className="w-full rounded-xl border border-stone-300 py-2.5 pr-3.5 pl-10 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold uppercase tracking-wider text-stone-700">
                    Custom Notes / Occasion <span className="font-normal text-stone-400">(optional)</span>
                  </label>
                  <textarea
                    rows={3}
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                    placeholder="Share any preferred colours, engraving text, or event date…"
                    className="mt-1 w-full rounded-xl border border-stone-300 p-3 text-sm text-stone-900 focus:border-amber-800 focus:outline-none"
                  />
                </div>

                <label className="flex cursor-pointer items-start gap-2.5 pt-1 text-xs text-stone-600">
                  <input
                    type="checkbox"
                    checked={consentGiven}
                    onChange={(e) => setConsentGiven(e.target.checked)}
                    className="mt-0.5 h-4 w-4 accent-amber-800"
                  />
                  <span>
                    I consent to Resin Art by Dhara storing my contact details to respond to my
                    enquiry on WhatsApp or email. See our{' '}
                    <Link to="/privacy" className="font-semibold text-amber-900 underline">
                      Privacy Notice
                    </Link>
                    .
                  </span>
                </label>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-full bg-emerald-700 px-6 py-3.5 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-800 disabled:opacity-60"
                >
                  <MessageCircle className="h-4 w-4" />
                  {isSubmitting
                    ? 'Saving & Redirecting to WhatsApp…'
                    : 'Submit & Chat on WhatsApp'}
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
