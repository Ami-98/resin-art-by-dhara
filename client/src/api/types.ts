export type RegionCode = 'GJ-IN' | 'US' | 'CA' | string;
export type ProductType = 'standard' | 'custom';
export type OptionType = 'select' | 'text' | 'image' | 'boolean';
export type ModifierKind = 'fixed' | 'percent';

export interface RegionDefinition {
  code: RegionCode;
  name: string;
  countryCode: string;
  currency: string;
  locale: string;
  flag?: string;
  timezones: string[];
}

export interface MoneyDto {
  amount: number; // minor units (paise/cents)
  currency: string;
}

export interface ShippingRule {
  mode: 'included' | 'flat' | 'freeAbove';
  amount: number;
  freeAbove?: number | null;
  note: string;
}

export interface TaxRule {
  display: string;
  label: string;
  ratePercent?: number | null;
}

export interface PricingExtra {
  id: string;
  label: string;
  modifier: {
    kind: ModifierKind;
    values?: Record<string, number>;
    value?: number;
  };
}

export interface BootstrapResponse {
  business: {
    name: string;
    tagline?: string;
    location?: string;
    ownerName?: string;
    contactEmail?: string | null;
    whatsAppUrl: string;
    customerReplyTemplate?: string;
    social: {
      instagram?: string | null;
      facebook?: string | null;
    };
  };
  regions: RegionDefinition[];
  defaultRegion: RegionCode;
  suggestedRegion?: RegionCode | null;
  suggestionSource?: string | null;
  detectedCountry?: string | null;
  shipping: Record<string, ShippingRule>;
  tax: Record<string, TaxRule>;
  extras: PricingExtra[];
  features: {
    enableCustomRequests: boolean;
    showRegionBanner: boolean;
  };
  leads: {
    requireAddress: boolean;
    privacyPolicyVersion: string;
    retentionMonths?: number;
  };
  uploads?: {
    maxFileSizeMb: number;
    maxFilesPerRequest: number;
  };
  catalog?: {
    defaultCategory: string;
    defaultLeadTimeDays: number;
    placeholderImageUrl: string;
  };
}

export interface CategoryDto {
  id: string;
  slug: string;
  name: string;
  description: string;
  sortOrder: number;
  imageUrl?: string | null;
}

export interface ProductSummaryDto {
  id: string;
  slug: string;
  name: string;
  category: string;
  type: ProductType;
  shortDescription: string;
  primaryImageUrl: string;
  primaryImageAlt: string;
  fromPrice: MoneyDto;
  taxLabel: string;
  hasCustomOptions: boolean;
  leadTimeDays: number;
  tags: string[];
  isFeatured: boolean;
}

export interface ProductListResponse {
  region: RegionCode;
  currency: string;
  items: ProductSummaryDto[];
}

export interface ProductImage {
  id: string;
  url: string;
  thumbUrl?: string | null;
  alt: string;
  sortOrder: number;
  isPrimary: boolean;
}

export interface RegionalModifierDto {
  kind: ModifierKind;
  amount?: number | null;
  currency?: string | null;
  percent?: number | null;
}

export interface OptionChoiceDto {
  id: string;
  label: string;
  modifier?: RegionalModifierDto | null;
}

export interface ProductOptionDto {
  id: string;
  label: string;
  type: OptionType;
  required: boolean;
  helpText?: string | null;
  constraints?: {
    maxLength?: number | null;
    placeholder?: string | null;
    maxFiles?: number | null;
    maxSizeMb?: number | null;
  } | null;
  choices: OptionChoiceDto[];
  modifier?: RegionalModifierDto | null;
}

export interface RegionalExtraDto {
  id: string;
  label: string;
  modifier: RegionalModifierDto;
}

export interface QuoteLine {
  label: string;
  amount: number;
}

export interface QuoteResult {
  productId: string;
  region: RegionCode;
  currency: string;
  total: number;
  lines: QuoteLine[];
  shippingNote: string;
  taxLabel: string;
  isEstimate: boolean;
}

export interface ProductDetailDto {
  id: string;
  slug: string;
  name: string;
  category: string;
  type: ProductType;
  shortDescription: string;
  description: string;
  images: ProductImage[];
  region: RegionCode;
  currency: string;
  basePrice: MoneyDto;
  initialQuote: QuoteResult;
  options: ProductOptionDto[];
  extras: RegionalExtraDto[];
  shipping: ShippingRule;
  tax: TaxRule;
  leadTimeDays: number;
  tags: string[];
  isFeatured: boolean;
}

export interface OptionSelectionInput {
  optionId: string;
  choiceId?: string | null;
  value?: string | null;
  provided?: boolean | null;
}

export interface TempUploadResult {
  tempId: string;
  previewUrl: string;
  sizeBytes: number;
}

export interface CreateContactPayload {
  name: string;
  phone: string;
  email: string;
  address?: string | null;
  message?: string | null;
  productSlug?: string | null;
  selectedRegion?: RegionCode;
  sourcePage?: string | null;
  consentGiven: boolean;
  website?: string;
}

export interface CreateCustomRequestPayload {
  productId: string;
  selectedRegion: RegionCode;
  selections: OptionSelectionInput[];
  extras: string[];
  tempImageIds: string[];
  notes?: string | null;
  contact: {
    name: string;
    phone: string;
    email: string;
    address?: string | null;
  };
  consentGiven: boolean;
  website?: string;
}

export interface LeadSubmissionResponse {
  id: string;
  reference: string;
  whatsappUrl: string;
  quotedPrice?: {
    total: number;
    currency: string;
    lines: QuoteLine[];
    shippingNote: string;
    taxLabel: string;
    pricingRulesVersion: number;
  } | null;
}

export type ProductStatus = 'draft' | 'published' | 'archived';
export type LeadStatus =
  | 'new'
  | 'contacted'
  | 'quoted'
  | 'confirmed'
  | 'inProgress'
  | 'delivered'
  | 'closed'
  | 'cancelled';

export interface AdminProfileDto {
  id: string;
  username: string;
  displayName: string;
  role: string;
}

export interface AdminDashboardDto {
  publishedProducts: number;
  draftProducts: number;
  archivedProducts: number;
  newContacts: number;
  totalContacts: number;
  newCustomRequests: number;
  totalCustomRequests: number;
  regionMismatches: number;
}

export interface RegionalBasePrice {
  amount: number;
  currency: string;
  isOverridden: boolean;
}

export interface AdminPriceModifier {
  kind: ModifierKind;
  values: Record<string, number>;
  value: number;
  derived: boolean;
}

export interface AdminOptionChoice {
  id: string;
  label: string;
  modifier?: AdminPriceModifier | null;
}

export interface AdminProductOption {
  id: string;
  label: string;
  type: OptionType;
  required: boolean;
  helpText?: string | null;
  constraints?: {
    maxLength?: number | null;
    placeholder?: string | null;
    maxFiles?: number | null;
    maxSizeMb?: number | null;
  } | null;
  choices: AdminOptionChoice[];
  modifier?: AdminPriceModifier | null;
}

export interface AdminProductDocument {
  id: string;
  slug: string;
  name: string;
  category: string;
  type: ProductType;
  status: ProductStatus;
  shortDescription: string;
  description: string;
  images: ProductImage[];
  basePrices: Record<string, RegionalBasePrice>;
  options: AdminProductOption[];
  leadTimeDays: number;
  tags: string[];
  isFeatured: boolean;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface PricingRulesDocument {
  id: string;
  version: number;
  baseRegion: string;
  regionRules: Record<
    string,
    {
      rate: number;
      markupPercent: number;
      rounding: {
        mode: 'none' | 'nearest' | 'charm';
        step: number;
        ending: number;
      };
    }
  >;
  extras: PricingExtra[];
  shipping: Record<string, ShippingRule>;
  tax: Record<string, TaxRule>;
}

export interface PricingMatrixResponse {
  regions: RegionDefinition[];
  rules: PricingRulesDocument;
  products: AdminProductDocument[];
}

export interface ContactDocument {
  id: string;
  reference: string;
  name: string;
  phone: string;
  email: string;
  address?: string | null;
  message?: string | null;
  productSlug?: string | null;
  productName?: string | null;
  detectedRegion?: string | null;
  selectedRegion: string;
  sourcePage?: string | null;
  status: LeadStatus;
  adminNotes: string;
  createdAt: string;
  meta: {
    ipHash?: string | null;
    userAgent?: string | null;
    regionMismatch: boolean;
  };
}

export interface CustomRequestDocument {
  id: string;
  reference: string;
  productId: string;
  productSlug: string;
  productNameSnapshot: string;
  detectedRegion?: string | null;
  selectedRegion: string;
  selections: {
    optionId: string;
    optionLabel: string;
    choiceId?: string | null;
    choiceLabel?: string | null;
    value?: string | null;
    provided?: boolean | null;
  }[];
  extras: string[];
  referenceImages: string[];
  notes?: string | null;
  contact: {
    name: string;
    phone: string;
    email: string;
    address?: string | null;
  };
  quote: {
    total: number;
    currency: string;
    lines: QuoteLine[];
    shippingNote: string;
    taxLabel: string;
    pricingRulesVersion: number;
  };
  status: LeadStatus;
  adminNotes: string;
  createdAt: string;
  meta: {
    ipHash?: string | null;
    userAgent?: string | null;
    regionMismatch: boolean;
  };
}

export interface AdminLeadsResponse {
  contacts: ContactDocument[];
  customRequests: CustomRequestDocument[];
}

export interface EditableSettingsPayload {
  business: {
    name: string;
    tagline?: string;
    location?: string;
    ownerName?: string;
    contactEmail?: string | null;
    whatsApp: {
      number: string;
      baseUrl: string;
      defaultMessage: string;
      includeReference: boolean;
      customerReplyTemplate?: string;
    };
    social: {
      instagram?: string | null;
      facebook?: string | null;
    };
  };
  regions: {
    default: string;
    fallbackForUnsupported: string;
    countryMap: Record<string, string>;
    definitions: RegionDefinition[];
    geoRules: {
      label: string;
      region: string;
      minLat: number;
      maxLat: number;
      minLng: number;
      maxLng: number;
    }[];
  };
  leads: {
    requireAddress: boolean;
    retentionMonths: number;
    privacyPolicyVersion: string;
    contactReferencePrefix?: string;
    customRequestReferencePrefix?: string;
    counterStartValue?: number;
    defaultPhoneCountryCode?: string;
    ipHashSalt?: string;
    maxUserAgentLength?: number;
    minNameLength?: number;
    minAddressLength?: number;
  };
  features: {
    enableCustomRequests: boolean;
    showRegionBanner: boolean;
  };
  seo: {
    publicBaseUrl?: string | null;
    titleSuffix?: string;
    defaultDescription: string;
    defaultImage?: string | null;
    placeholderImage?: string;
    sitemapStaticPaths?: string[];
    robotsDisallowPaths?: string[];
  };
  uploads?: {
    maxFileSizeMb: number;
    maxFilesPerRequest: number;
    tempFilePrefix: string;
    referenceFilePrefix: string;
    stripExifMetadata: boolean;
  };
  catalog?: {
    defaultCategory: string;
    defaultLeadTimeDays: number;
    defaultBasePriceMinor: number;
    placeholderImageUrl: string;
    fallbackSlug: string;
  };
  maintenance?: {
    intervalHours: number;
    keepBackupsCount: number;
    orphanedTempFileMaxAgeHours: number;
    maxRecentSettingsBackups: number;
  };
  configFilePath: string;
  recentBackups: string[];
}

export interface BackupArchiveInfo {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
}

export interface MaintenanceReport {
  backupCreated?: BackupArchiveInfo | null;
  expiredContactsDeleted: number;
  expiredRequestsDeleted: number;
  orphanedTempFilesDeleted: number;
  oldBackupsPruned: number;
}



