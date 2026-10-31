# Resin Art by Dhara — Full-Stack Web Application

Handcrafted resin art showcase, regional custom pricing engine (**Gujarat/India INR · USA USD · Canada CAD**), WhatsApp lead capture, and studio admin console built with **React 19 + TypeScript (Vite)** and **ASP.NET Core (.NET 9) Minimal APIs** using atomic file-based JSON storage.

---

## Project Structure

```text
d:\ResinArtByDhara
├─ client/                          # React 19 + TypeScript + Tailwind CSS (Vite)
│  └─ src/
│     ├─ api/                       # Typed REST client & DTO contracts
│     ├─ components/                # AppLayout (header, RegionBanner, footer)
│     ├─ features/
│     │  ├─ region/                 # RegionProvider, RegionSelector, IP + Geolocation detection
│     │  ├─ catalog/                # HomePage, ProductGallery, ProductCard, ProductDetail + live quote
│     │  ├─ contact/                # ContactPage (saves lead -> redirects to WhatsApp "Hi")
│     │  ├─ legal/                  # PrivacyPage (DPDP / PIPEDA notice)
│     │  └─ admin/                  # AdminLayout, Products, Pricing Matrix, Leads Inbox, Settings
│     └─ lib/                       # Integer minor-unit currency formatter
├─ server/
│  ├─ ResinArt.Api/                 # ASP.NET Core Web API + SPA Host + Open Graph meta injection
│  │  ├─ Features/
│  │  │  ├─ Catalog/                # Categories & Products endpoints + models
│  │  │  ├─ Pricing/                # Deterministic PricingEngine + /api/pricing/quote
│  │  │  ├─ Regions/                # RegionResolver, IpCountryLookup, /api/bootstrap
│  │  │  ├─ Leads/                  # Contacts, CustomRequests, phone normalization, rate limiting
│  │  │  ├─ Auth/                   # Cookie auth + PBKDF2 password hashing + lockout
│  │  │  ├─ Admin/                  # Product/Image CRUD, Pricing Matrix, Leads CSV, Backups
│  │  │  └─ Seo/                    # Server-side Open Graph / JSON-LD injection + sitemap.xml
│  │  ├─ Infrastructure/
│  │  │  ├─ Storage/                # IDocumentStore<T>, JsonFileStore<T>, DataRoot, DataSeeder
│  │  │  ├─ Images/                 # CustomerUploadStore (magic-byte check + JPEG/PNG EXIF strip)
│  │  │  ├─ Settings/               # SettingsService (atomic appsettings.Business.json + Reload)
│  │  │  ├─ Notifications/          # Non-blocking Channel<T> background lead notification worker
│  │  │  └─ Jobs/                   # DataMaintenanceService (ZIP snapshots + retention cleanup)
│  │  └─ appsettings.json           # Default configuration schema
│  └─ ResinArt.Tests/               # xUnit unit & WebApplicationFactory integration test suite
└─ Dockerfile                       # Multi-stage build (Node 20 -> .NET 9 SDK -> ASP.NET runtime)
```

---

## Running Locally

### 1. Build the Frontend & Run the Unified Server
The Vite build outputs directly to `server/ResinArt.Api/wwwroot`, so a single `dotnet run` serves the React SPA, `/api/*`, `/uploads/*`, and injects Open Graph link-preview tags for WhatsApp/Instagram.

```powershell
# 1. Build React client (only needed after frontend edits)
cd d:\ResinArtByDhara\client
npm install
npm run build

# 2. Run ASP.NET Core API + SPA host
cd d:\ResinArtByDhara\server\ResinArt.Api
dotnet run
```

- **Storefront:** `http://localhost:5224`
- **Admin Console:** `http://localhost:5224/admin`
  - Default studio login: **`dhara`** / **`ResinArt@2026`** (override via `Admin__InitialUsername` and `Admin__InitialPassword` environment variables before first startup).

### 2. Run the Automated Test Suite
```powershell
cd d:\ResinArtByDhara\server
dotnet test
```

---

## Persistent Data Root & Backups

All runtime state is stored outside the deployment folder in `Storage:RootPath` (`d:\ResinArtByDhara\data` in development, `/var/resinart-data` in Docker):

- `db/products.json`, `db/categories.json`, `db/pricing-rules.json`
- `db/contacts.json`, `db/custom-requests.json`, `db/counters.json`, `db/admin-users.json`
- `config/appsettings.Business.json` (admin-edited configuration layered over `appsettings.json`)
- `uploads/products/*`, `uploads/requests/*`, `uploads/temp/*`
- `backups/snapshot-*.zip` (downloadable directly from `/admin/settings`)

### Restore Runbook
1. Stop the container / process.
2. Extract `snapshot-YYYYMMDD-HHMMSS-*.zip` into the persistent data root (`/var/resinart-data/`).
3. Start the container / process.
