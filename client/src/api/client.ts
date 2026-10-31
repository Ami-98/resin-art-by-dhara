import type {
  AdminDashboardDto,
  AdminLeadsResponse,
  AdminProductDocument,
  AdminProfileDto,
  BackupArchiveInfo,
  BootstrapResponse,
  CategoryDto,
  CreateContactPayload,
  CreateCustomRequestPayload,
  EditableSettingsPayload,
  LeadStatus,
  LeadSubmissionResponse,
  MaintenanceReport,
  OptionSelectionInput,
  PricingMatrixResponse,
  PricingRulesDocument,
  ProductDetailDto,
  ProductListResponse,
  ProductStatus,
  ProductType,
  QuoteResult,
  RegionCode,
  RegionalBasePrice,
  TempUploadResult,
} from './types';

export class ApiError extends Error {
  status: number;
  fieldErrors?: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    const firstFieldErr =
      body?.errors && typeof body.errors === 'object'
        ? (Object.values(body.errors).flat()[0] as string | undefined)
        : undefined;
    throw new ApiError(
      firstFieldErr || body?.title || body?.message || `Request failed (${res.status})`,
      res.status,
      body?.errors
    );
  }
  return res.json() as Promise<T>;
}

export const api = {
  getBootstrap: () => request<BootstrapResponse>('/api/bootstrap'),

  resolveCoordinates: (lat: number, lng: number) =>
    request<{ regionCode: RegionCode; matched: boolean; label?: string | null }>('/api/regions/resolve', {
      method: 'POST',
      body: JSON.stringify({ lat, lng }),
    }),

  getCategories: () => request<CategoryDto[]>('/api/categories'),

  getProducts: (params: {
    region: RegionCode;
    category?: string;
    type?: ProductType;
    featured?: boolean;
    q?: string;
  }) => {
    const sp = new URLSearchParams();
    sp.set('region', params.region);
    if (params.category) sp.set('category', params.category);
    if (params.type) sp.set('type', params.type);
    if (params.featured !== undefined) sp.set('featured', String(params.featured));
    if (params.q) sp.set('q', params.q);
    return request<ProductListResponse>(`/api/products?${sp.toString()}`);
  },

  getProductBySlug: (slug: string, region: RegionCode) =>
    request<ProductDetailDto>(
      `/api/products/${encodeURIComponent(slug)}?region=${encodeURIComponent(region)}`
    ),

  calculateQuote: (payload: {
    productId: string;
    region: RegionCode;
    selections: OptionSelectionInput[];
    extras: string[];
  }) =>
    request<QuoteResult>('/api/pricing/quote', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  submitContact: (payload: CreateContactPayload) =>
    request<LeadSubmissionResponse>('/api/contacts', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  submitCustomRequest: (payload: CreateCustomRequestPayload) =>
    request<LeadSubmissionResponse>('/api/custom-requests', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  uploadTempImage: async (file: File): Promise<TempUploadResult> => {
    const form = new FormData();
    form.append('file', file);
    const res = await fetch('/api/uploads/temp', {
      method: 'POST',
      body: form,
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      const firstErr =
        body?.errors && typeof body.errors === 'object'
          ? (Object.values(body.errors).flat()[0] as string | undefined)
          : undefined;
      throw new ApiError(firstErr || body?.title || 'Image upload failed.', res.status, body?.errors);
    }
    return res.json() as Promise<TempUploadResult>;
  },

  // Admin Auth
  getAdminMe: () => request<AdminProfileDto>('/api/auth/me'),

  adminLogin: (username: string, password: string) =>
    request<AdminProfileDto>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),

  adminLogout: () =>
    request<{ message: string }>('/api/auth/logout', {
      method: 'POST',
    }),

  // Admin Dashboard & Products
  getAdminDashboard: () => request<AdminDashboardDto>('/api/admin/dashboard'),

  getAdminProducts: (params?: { status?: ProductStatus; category?: string; q?: string }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set('status', params.status);
    if (params?.category) sp.set('category', params.category);
    if (params?.q) sp.set('q', params.q);
    const qs = sp.toString();
    return request<AdminProductDocument[]>(`/api/admin/products${qs ? `?${qs}` : ''}`);
  },

  createAdminProduct: (payload: Partial<AdminProductDocument>) =>
    request<AdminProductDocument>('/api/admin/products', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateAdminProduct: (id: string, payload: Partial<AdminProductDocument>) =>
    request<AdminProductDocument>(`/api/admin/products/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  updateAdminProductStatus: (id: string, status: ProductStatus) =>
    request<AdminProductDocument>(`/api/admin/products/${encodeURIComponent(id)}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),

  deleteAdminProduct: (id: string, hard = false) =>
    request<unknown>(`/api/admin/products/${encodeURIComponent(id)}?hard=${hard}`, {
      method: 'DELETE',
    }),

  uploadAdminProductImage: async (id: string, file: File, alt?: string): Promise<AdminProductDocument> => {
    const form = new FormData();
    form.append('file', file);
    if (alt) form.append('alt', alt);
    const res = await fetch(`/api/admin/products/${encodeURIComponent(id)}/images`, {
      method: 'POST',
      body: form,
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new ApiError(body?.title || 'Image upload failed.', res.status, body?.errors);
    }
    return res.json() as Promise<AdminProductDocument>;
  },

  setAdminProductPrimaryImage: (id: string, imageId: string) =>
    request<AdminProductDocument>(
      `/api/admin/products/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}/primary`,
      { method: 'PUT' }
    ),

  deleteAdminProductImage: (id: string, imageId: string) =>
    request<AdminProductDocument>(
      `/api/admin/products/${encodeURIComponent(id)}/images/${encodeURIComponent(imageId)}`,
      { method: 'DELETE' }
    ),

  // Admin Pricing
  getPricingMatrix: () => request<PricingMatrixResponse>('/api/admin/pricing/matrix'),

  savePricingMatrix: (
    rows: { productId: string; basePrices: Record<string, RegionalBasePrice> }[]
  ) =>
    request<{ updated: number }>('/api/admin/pricing/matrix', {
      method: 'PUT',
      body: JSON.stringify({ rows }),
    }),

  updatePricingRules: (rules: PricingRulesDocument) =>
    request<PricingRulesDocument>('/api/admin/pricing/rules', {
      method: 'PUT',
      body: JSON.stringify(rules),
    }),

  recalculatePrices: (dryRun: boolean, forceAll = false) =>
    request<{
      dryRun: boolean;
      count: number;
      changes: {
        productId: string;
        productName: string;
        region: string;
        currency: string;
        oldAmount: number;
        newAmount: number;
      }[];
    }>('/api/admin/pricing/recalculate', {
      method: 'POST',
      body: JSON.stringify({ dryRun, forceAll }),
    }),

  // Admin Leads Inbox
  getAdminLeads: (status?: LeadStatus) =>
    request<AdminLeadsResponse>(
      `/api/admin/leads${status ? `?status=${encodeURIComponent(status)}` : ''}`
    ),

  updateAdminLead: (kind: 'contacts' | 'requests', id: string, status: LeadStatus, adminNotes: string) =>
    request<unknown>(`/api/admin/leads/${kind}/${encodeURIComponent(id)}`, {
      method: 'PATCH',
      body: JSON.stringify({ status, adminNotes }),
    }),

  deleteAdminLead: (kind: 'contacts' | 'requests', id: string) =>
    request<{ deleted: boolean }>(`/api/admin/leads/${kind}/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    }),

  // Admin Configuration (appsettings.Business.json)
  getAdminSettings: () => request<EditableSettingsPayload>('/api/admin/settings'),

  updateAdminSettings: (payload: Omit<EditableSettingsPayload, 'configFilePath' | 'recentBackups'>) =>
    request<{
      savedAt: string;
      backupFileName?: string | null;
      settings: EditableSettingsPayload;
    }>('/api/admin/settings', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  // Admin Backups & Maintenance
  getAdminBackups: () => request<BackupArchiveInfo[]>('/api/admin/backups'),

  runAdminMaintenance: () =>
    request<MaintenanceReport>('/api/admin/backups/run', {
      method: 'POST',
    }),
};
