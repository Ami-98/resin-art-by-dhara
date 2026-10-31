/**
 * Formats integer minor units (paise/cents) into a localised currency string.
 * Strips trailing ".00" on whole INR amounts (e.g. ₹2,899 instead of ₹2,899.00) while preserving $.99 charm prices.
 */
export function formatMoney(amountMinor: number, currency: string, locale = 'en-IN'): string {
  const major = amountMinor / 100;
  const hasSubunits = amountMinor % 100 !== 0;

  try {
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: hasSubunits ? 2 : 0,
      maximumFractionDigits: 2,
    }).format(major);
  } catch {
    return `${currency} ${major.toFixed(hasSubunits ? 2 : 0)}`;
  }
}
