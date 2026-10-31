import React from 'react';
import { useRegion } from '../region/RegionContext';

export const PrivacyPage: React.FC = () => {
  const { bootstrap } = useRegion();
  const biz = bootstrap?.business.name ?? 'Resin Art by Dhara';

  return (
    <div className="mx-auto max-w-3xl px-4 py-12 sm:px-6">
      <div className="rounded-3xl border border-stone-200 bg-white p-8 shadow-xs">
        <span className="text-xs font-semibold uppercase tracking-widest text-amber-800">
          Privacy &amp; Data Protection (Policy {bootstrap?.leads.privacyPolicyVersion ?? '2026-10'})
        </span>
        <h1 className="mt-1 text-3xl font-bold text-stone-900">Privacy Notice — {biz}</h1>
        <div className="mt-4 space-y-4 text-sm leading-relaxed text-stone-700">
          <p>
            When you submit our Contact Form or a Custom Product Request, we collect your name,
            phone/WhatsApp number, email address, optional delivery address, and any customization
            choices or reference photos you provide.
          </p>
          <p>
            <strong>How we use your information:</strong> Solely to prepare your custom resin art
            quote, correspond with you on WhatsApp or email, and ship your order. We never sell or
            share your details with third-party marketers.
          </p>
          <p>
            <strong>Photo &amp; location privacy:</strong> Reference photos uploaded for custom
            products have camera EXIF/GPS metadata stripped automatically on our server. Raw IP
            addresses are never stored in our database.
          </p>
          <p>
            <strong>Data retention &amp; deletion:</strong> Enquiry records are retained for up to{' '}
            {bootstrap?.leads.retentionMonths ?? 24} months and can be deleted earlier at any time
            by messaging us on WhatsApp or emailing {bootstrap?.business.contactEmail ?? 'our studio'}.
          </p>
        </div>
      </div>
    </div>
  );
};
