# DataHub - Architecture & Notes

> **Living document.** Update as the project evolves. Date-stamp meaningful decisions in the Decision Log.

---

## 1. Overview

DataHub is a self-contained, local-first application for **collecting, viewing, querying, and analyzing** many kinds of data:

- Financial / market data
- Geospatial / mapping
- Historical / religious / reference
- News / current events
- Web scraping & external API ingestion
- Anything else (generic, flexible)

It's designed to start small (running locally) but scale into a larger personal/team-grade hub.

---

## 2. Tech Stack

| Layer | Tech |
|-------|------|
| Backend | **.NET 10**, ASP.NET Core Web API (controllers) |
| Database | **SQL Server** (existing instance), **EF Core 10** code-first migrations |
| Auth | **JWT** access tokens + **httpOnly cookie** refresh tokens, ASP.NET Identity `PasswordHasher<T>` |
| Frontend | **React 18 + TypeScript**, **Vite**, **MUI** |
| HTTP client | **axios** with auto-refresh interceptor |
| Routing | **react-router-dom** |

---

## 3. Repository Structure

```
/datahub
├── DataHub.sln
├── .gitignore
├── .env.example
├── src/
│   ├── DataHub.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/          # HealthController, AuthController, UsersController, DataController
│   │   ├── Properties/launchSettings.json
│   │   ├── Program.cs            # Composition root: DI, auth, CORS, migrate+seed, pipeline
│   │   ├── appsettings.json      # Defaults (no secrets)
│   │   └── appsettings.Development.json  # GITIGNORED — dev secrets (connection, JWT key)
│   ├── DataHub.Core/             # Pure domain: entities, interfaces, DTOs, constants
│   │   ├── Entities/             # User, Role, Permission, UserRole, RolePermission, RefreshToken, DataSource, DataEntry
│   │   ├── Interfaces/           # IAuthService, ITokenService, IUserService
│   │   ├── DTOs/                 # Auth, Users, Data request/response records
│   │   └── Constants/Permissions.cs
│   ├── DataHub.Infrastructure/   # EF Core + concrete services
│   │   ├── Data/
│   │   │   ├── DataHubDbContext.cs
│   │   │   ├── Configurations/   # IEntityTypeConfiguration<T> for every entity
│   │   │   └── Migrations/       # EF Core migrations (InitialCreate, etc.)
│   │   ├── Services/             # TokenService, AuthService, UserService, JwtOptions
│   │   ├── Seeding/DbSeeder.cs   # Idempotent seed: permissions, Admin role, admin user
│   │   └── InfrastructureServiceRegistration.cs  # AddDataHubInfrastructure(services, config)
│   └── datahub-ui/               # React + Vite + TS + MUI
│       ├── src/
│       │   ├── api/              # axios client + endpoint wrappers
│       │   ├── auth/             # AuthContext, ProtectedRoute
│       │   ├── components/Layout.tsx
│       │   ├── pages/            # Login, Dashboard, DataSources, DataEntries, admin/Users
│       │   ├── theme/theme.ts
│       │   ├── App.tsx
│       │   └── main.tsx
│       └── vite.config.ts        # Proxies /api → https://localhost:7283
└── docs/
    └── ARCHITECTURE.md           # ← this file
```

**Project references:** `DataHub.Api → DataHub.Infrastructure → DataHub.Core`
Core has zero external dependencies. Infrastructure owns EF, identity, JWT. Api wires it all together.

---

## 4. Authentication & Authorization

### Flow

1. `POST /api/auth/login` { email, password }
   → returns `{ accessToken, accessTokenExpiresAt, user }`
   → sets `dh_refresh` httpOnly cookie (path=`/api/auth`, 7 days)
2. Client stores **access token in memory only** (React context).
3. `POST /api/auth/refresh` — reads cookie, rotates refresh token, returns new access token.
4. `POST /api/auth/logout` — revokes refresh token, clears cookie.

### Token details

- **Access token**: JWT signed with HS256, includes `sub` (user id), `email`, `role` claims, and one `permission` claim per permission. Default lifetime 60 min in dev, 15 in prod.
- **Refresh token**: 64 random bytes, base64. Stored in `RefreshTokens` table. **Rotated on every refresh** — old one is revoked and a new one issued.

### Authorization model

- `[Authorize]` requires any authenticated user.
- Permission-based policies are registered for every value in `DataHub.Core.Constants.Permissions.All`. Use them as:
  ```csharp
  [Authorize(Policy = Permissions.DataWrite)]
  ```
- The frontend `useAuth().hasPermission("data:write")` mirrors this for UI gating.

### Default permissions

`users:read`, `users:manage`, `roles:manage`, `data:read`, `data:write`, `sources:manage`, `sports:read`, `sports:manage`, `geo:read`, `geo:manage`

### Seeded data (idempotent, on every startup)

- All default permissions
- `Admin` role with **every** permission attached
- Admin user `tedlucas@outlook.com` / `DataZilla.247` (hashed with ASP.NET Identity `PasswordHasher`)

> **Note:** Self-registration is disabled. New users must be created by an admin via `POST /api/users`.

---

## 5. Data Model

### AuditableEntity base class

All persisted domain entities (with a few intentional exceptions, see below) inherit from `DataHub.Core.Entities.AuditableEntity`, which provides:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Primary key, defaults to `Guid.NewGuid()` |
| `IsActive` | `bool` | Soft-delete flag. Services set `false` instead of hard-deleting. Queries should filter where appropriate. |
| `Source` | `string?` (max 256) | Free-form provenance tag, e.g. `"seed:mlb-initial"`, `"seed:bootstrap"`, `"manual"`, `"import:csv:2025-Q4"`. Lets us trace where any row originated. |
| `CreatedAt` | `DateTime` (UTC) | Stamped on insert by `DataHubDbContext.SaveChangesAsync` |
| `CreatedBy` | `string?` (max 256) | Email of the acting user; `"system"` for background work; `"design-time"` for `dotnet ef` operations. Stamped on insert and never overwritten on update. |
| `UpdatedAt` | `DateTime` (UTC) | Stamped on insert and on every update |
| `UpdatedBy` | `string?` (max 256) | Same semantics as `CreatedBy` but updated on every change |

**How auditing works:**
- `DataHub.Core.Interfaces.ICurrentUser` abstracts "who is acting now"; the API layer implements it as `HttpContextCurrentUser` (reads the JWT email claim). Infrastructure has no ASP.NET dependency.
- `DataHubDbContext` takes an optional `ICurrentUser` via DI; if absent (e.g., design-time factory), it falls back to a `NullCurrentUser` returning `"design-time"` / `"system"`.
- On `SaveChangesAsync`, the context walks the change tracker: `Added` entries get `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` stamped; `Modified` entries get only `UpdatedAt/UpdatedBy` (Created* fields are forced `IsModified=false` to prevent accidental overwrites).
- Explicit values are preserved: stampers use `??=` for Created* so seeders/imports can set them directly when backfilling historical data.

**Exceptions (do NOT inherit AuditableEntity):**
- `RefreshToken` — short-lived, already has its own lifecycle fields (`CreatedAt`, `ExpiresAt`, `RevokedAt`).
- `UserRole`, `RolePermission` — pure join tables with composite PKs and no independent lifecycle.

### Auth tables

| Table | Key fields |
|-------|-----------|
| `Users` | Email (unique), PasswordHash, FirstName, LastName + [AuditableEntity] |
| `Roles` | Name (unique), Description + [AuditableEntity] |
| `Permissions` | Name (unique), Description + [AuditableEntity] |
| `UserRoles` | (UserId, RoleId) composite PK |
| `RolePermissions` | (RoleId, PermissionId) composite PK |
| `RefreshTokens` | Id, UserId, Token (unique), ExpiresAt, CreatedAt, RevokedAt |

### Data tables (flexible-first)

| Table | Key fields |
|-------|-----------|
| `DataSources` | Name, Type, Description, ConfigJson (nvarchar(max)) + [AuditableEntity] |
| `DataEntries` | DataSourceId (FK, nullable), Category, Tags (csv), PayloadJson (nvarchar(max)), CreatedByUserId (FK, nullable) + [AuditableEntity] |

Note: `DataEntries.CreatedByUserId` (typed FK to `User`) is intentionally separate from the inherited `CreatedBy` (email string). The FK is the structured pointer for joins/cascade; the inherited string is a denormalized human-readable audit trail consistent with all other tables.

### Sports tables

See §12 for the full Sports domain model. All Sports entities (`Sport`, `SportLevel`, `League`, `Conference`, `Venue`, `Team`, `TeamSeason`) inherit `AuditableEntity`.

**Strategy:** `DataEntries` accepts arbitrary JSON in `PayloadJson` so any domain can be ingested immediately. As patterns stabilize for a domain (e.g., stock quotes, news articles), promote it to a **typed entity** with proper columns and indexes, and migrate.

### Indexes

- `Users.Email` unique, `Roles.Name` unique, `Permissions.Name` unique, `RefreshTokens.Token` unique
- `DataSources.Name` indexed; `DataEntries.Category` and `DataEntries.CreatedAt` indexed.

---

## 6. API Endpoints

> Living list. Add to it as endpoints land.

### Public / auth
- `POST /api/auth/login` — login, returns access token + sets refresh cookie
- `POST /api/auth/refresh` — issues a new access token from refresh cookie
- `POST /api/auth/logout` — revokes refresh token
- `GET  /api/health` — unauthenticated health check

### Users (admin)
- `GET    /api/users`            (perm `users:read`)
- `GET    /api/users/{id}`       (perm `users:read`)
- `POST   /api/users`            (perm `users:manage`)
- `PUT    /api/users/{id}`       (perm `users:manage`)
- `DELETE /api/users/{id}`       (perm `users:manage`)

### Data sources
- `GET    /api/data-sources`      (perm `data:read`)
- `GET    /api/data-sources/{id}` (perm `data:read`)
- `POST   /api/data-sources`      (perm `sources:manage`)
- `PUT    /api/data-sources/{id}` (perm `sources:manage`)
- `DELETE /api/data-sources/{id}` (perm `sources:manage`)

### Data entries
- `GET  /api/data-entries`      (perm `data:read`; filters: sourceId, category, tag, skip, take)
- `GET  /api/data-entries/{id}` (perm `data:read`)
- `POST /api/data-entries`      (perm `data:write`)

### Sports (read = `sports:read`, write = `sports:manage`)
- `GET    /api/sports`                                        list sports
- `GET    /api/sports/{id}`                                   sport details
- `POST   /api/sports`                                        create sport
- `PUT    /api/sports/{id}`                                   update sport
- `DELETE /api/sports/{id}`                                   soft-delete
- `GET    /api/sports/{sportId}/levels`                       list levels
- `POST   /api/sports/{sportId}/levels`                       create level
- `GET    /api/sport-levels/{id}`, `PUT`, `DELETE`
- `GET    /api/sport-levels/{sportLevelId}/leagues`           list leagues
- `POST   /api/sport-levels/{sportLevelId}/leagues`           create league
- `GET    /api/leagues/{id}`, `PUT`, `DELETE`
- `GET    /api/leagues/{leagueId}/conferences`                list conferences
- `POST   /api/leagues/{leagueId}/conferences`                create conference
- `GET    /api/conferences/{id}`, `PUT`, `DELETE`
- `GET    /api/leagues/{leagueId}/teams`                      list teams in a league
- `POST   /api/leagues/{leagueId}/teams`                      create team
- `GET    /api/teams`, `GET /api/teams/{id}`, `PUT`, `DELETE`
- `GET    /api/venues`, `GET /api/venues/{id}`, `POST`, `PUT`, `DELETE`

### Geo (read = `geo:read`, write = `geo:manage`)
- `GET    /api/geo/countries`                                 list countries
- `GET    /api/geo/countries/{id}`                            country details
- `GET    /api/geo/countries/by-iso2/{iso2}`                  lookup by ISO-2 (e.g. `US`)
- `POST   /api/geo/countries`                                 create country
- `PUT    /api/geo/countries/{id}`                            update country
- `DELETE /api/geo/countries/{id}`                            soft-delete
- `PUT    /api/geo/countries/{id}/geometry`                   set geometry from GeoJSON
- `GET    /api/geo/countries/{countryId}/states`              list states in a country
- `POST   /api/geo/countries/{countryId}/states`              create state under a country
- `GET    /api/geo/states/{id}`, `PUT`, `DELETE`, `PUT {id}/geometry`
- `GET    /api/geo/states/{stateId}/counties`                 list counties in a state
- `POST   /api/geo/states/{stateId}/counties`                 create county under a state
- `GET    /api/geo/counties/{id}`, `PUT`, `DELETE`, `PUT {id}/geometry`

#### Geo cache (static, no auth)
Pre-rendered GeoJSON files emitted to `wwwroot/geo-cache/` and served as `application/geo+json` with long-cache headers (`max-age=31536000, immutable`). The frontend reads geometry from here rather than from the API DTOs (which omit raw geometry to keep responses small).

```
/geo-cache/countries/{id}.geojson              single country feature
/geo-cache/states/{id}.geojson                 single state feature
/geo-cache/counties/{id}.geojson               single county feature
/geo-cache/states/bundle-{countryId}.geojson   FeatureCollection of all states in a country
/geo-cache/counties/bundle-{stateId}.geojson   FeatureCollection of all counties in a state
```

> See `src/DataHub.Api/DataHub.Api.http` for runnable request examples.

---

## 7. Frontend

### Structure
- **`api/client.ts`** — axios instance, in-memory access token, request interceptor adds Bearer header, response interceptor refreshes on 401 (single-flight).
- **`api/endpoints.ts`** — typed wrappers (`authApi`, `usersApi`, etc.).
- **`auth/AuthContext.tsx`** — `useAuth()` exposes `{ user, loading, login, logout, hasPermission }`. On mount it attempts a silent refresh (in case the refresh cookie is still valid).
- **`auth/ProtectedRoute.tsx`** — redirects to `/login` if not authenticated.
- **`components/Layout.tsx`** — MUI AppBar + permanent Drawer with permission-aware nav items + user menu.
- **`pages/*`** — Login, Dashboard, DataSources, DataEntries, admin/Users.

### Dev experience
- Frontend dev server: **`http://localhost:5173`**
- API: **`https://localhost:7283`** (https) / `http://localhost:5275` (http) — see `src/DataHub.Api/Properties/launchSettings.json`
- Vite proxy in `vite.config.ts` forwards `/api/*` → `https://localhost:7283` so cookies stay same-origin from the browser's view.

### Running locally

Two terminals:
```bash
# 1) API
dotnet run --project src/DataHub.Api

# 2) UI
cd src/datahub-ui && npm run dev
```
Open http://localhost:5173 and log in with the seeded admin.

> First run: `dotnet run` will auto-`Migrate()` the DB and seed permissions/role/admin. If the SQL connection fails, fix `appsettings.Development.json` and retry.

---

## 8. Conventions & Patterns

### Backend
- **Layered**: Core (pure) → Infrastructure (EF, JWT, hashing) → Api (HTTP + composition).
- **Entities** inherit `DataHub.Core.Entities.AuditableEntity` unless they are short-lived (e.g., `RefreshToken`) or pure join tables (`UserRole`, `RolePermission`). See §5.
- **Soft-delete by default**: services set `IsActive = false` rather than calling `Remove`. Read queries filter on `IsActive` where it makes sense; admin-facing read endpoints may include inactive rows.
- **Provenance via `Source`**: every seeder, importer, or batch tool should set the `Source` string (e.g. `"seed:mlb-initial"`, `"import:nfl-2024-rosters"`) so origins are traceable.
- **Audit auto-stamping**: never set `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy` manually in services — `DataHubDbContext.SaveChangesAsync` does it via `ICurrentUser`. Seeders may set them explicitly to backfill historical data.
- **JSON columns** are typed as `nvarchar(max)` and named with `Json` suffix (e.g., `PayloadJson`).
- **Permissions** are referenced via the strongly-typed `DataHub.Core.Constants.Permissions` constants — never hard-code permission strings in controllers.
- **DTOs are `record`** types in `DataHub.Core.DTOs.*` namespaces.
- **Services** are registered in `InfrastructureServiceRegistration.AddDataHubInfrastructure`.
- **DbContext** auto-discovers `IEntityTypeConfiguration<T>` from the Infrastructure assembly.
- **Auth in controllers**: prefer `[Authorize(Policy = Permissions.SomePermission)]` over role checks.

### Adding a new entity
1. Add the entity class under `DataHub.Core/Entities/`, inheriting `AuditableEntity` (don't redeclare `Id`/`IsActive`/`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy`/`Source`).
2. Add an `IEntityTypeConfiguration<T>` in `DataHub.Infrastructure/Data/Configurations/` (a `*Configurations.cs` file per module is fine).
3. Add a `DbSet<T>` in `DataHubDbContext`. The reflection loop in `OnModelCreating` will apply `HasMaxLength(256)` to all audit string columns automatically.
4. Create a migration: `dotnet ef migrations add <Name> -p src/DataHub.Infrastructure -s src/DataHub.Api -o Data/Migrations`.
5. Add DTOs in `DataHub.Core/DTOs/`, a service interface in `Core/Interfaces/`, an implementation in `Infrastructure/Services/`, and a controller in `Api/Controllers/`.
6. In services, implement deletes as `entity.IsActive = false` followed by `SaveChangesAsync` — do not call `db.Remove(entity)` unless you explicitly need a hard delete.
7. Update this doc's API Endpoints section and add a runnable example to `DataHub.Api.http`.

### Adding a new permission
1. Add the constant to `DataHub.Core.Constants.Permissions` and include it in `Permissions.All`.
2. It will be auto-seeded into the DB on next startup, auto-attached to the Admin role, and auto-registered as a policy.
3. Use it in a controller: `[Authorize(Policy = Permissions.MyNewPerm)]`.

### Frontend
- Centralize HTTP in `src/api/*`. Components call **typed** wrappers, not raw axios.
- Use `useAuth().hasPermission(...)` to conditionally render UI elements that require a permission.
- New protected pages: add a route under the `ProtectedRoute` block in `App.tsx`, and (if it should appear in the sidebar) add it to `navItems` in `Layout.tsx`.

### Secrets & config
- **Never** commit `appsettings.Development.json` or `appsettings.Production.json`.
- `appsettings.json` (committed) holds only structural defaults with empty/placeholder secret values.
- Production: prefer environment variables (mapped via `Key__Subkey` syntax).

---

## 9. Decision Log

> Date-stamp every meaningful decision. Briefly explain *why*.

- **2026-05-22 — Repo layout: single mono-repo with `src/DataHub.Api`, `src/DataHub.Core`, `src/DataHub.Infrastructure`, `src/datahub-ui` and `docs/`.**
  Keeps backend + frontend together for solo dev velocity. Can split later if needed.
- **2026-05-22 — .NET 10 + ASP.NET Core controllers (not Minimal APIs).**
  User chose .NET 10 explicitly. Controllers chosen for clearer organization as the surface area grows.
- **2026-05-22 — EF Core code-first with `DbContext.Database.MigrateAsync()` on startup.**
  Frictionless local-first workflow. Will revisit if migrations become risky in prod.
- **2026-05-22 — Auth: JWT (HS256) access tokens in memory + httpOnly refresh cookie, with refresh token rotation.**
  XSS resistance for the long-lived token; access token kept short-lived; rotation limits reuse if leaked.
- **2026-05-22 — Permissions-as-policies model (not role-as-policy).**
  More granular and future-proof. Roles bundle permissions; controllers check permissions.
- **2026-05-22 — Admin-only user creation (no self-registration).**
  This is a personal/team hub, not a public app.
- **2026-05-22 — Flexible-first data model: `DataEntries.PayloadJson`.**
  Lets us ingest any domain immediately. Typed entities will be promoted from this pattern as domains stabilize.
- **2026-05-22 — Seed admin = `tedlucas@outlook.com`. Password stored hashed only; raw value lives only in this design note and the seeder constant (which writes the hash).**
  Note: change `DbSeeder.DefaultAdminPassword` before any non-local deployment.
- **2026-05-22 — Phase 2 sports taxonomy: Sport → Level → League → (Conference) → Team.**
  Level is per-Sport so dropdowns stay sensible; League disambiguates team names ("Cardinals"); Conference is optional and league-specific. See §12.1.
- **2026-05-22 — Bitemporal `EffectiveFrom` / `EffectiveTo` on fact-bearing rows.**
  Enables historical truth without destructive edits (team relocations, league changes) and powers the universal time slider. See §12.1.3.
- **2026-05-22 — Map-centric UX with three interchangeable viewers (Map / Grid / Dashboard) sharing filters and time window.**
  Map is the primary view; Grid is the power-user fallback; Dashboard is for aggregates. See §12.3.
- **2026-05-22 — Geo scope: US-only for Phase 2 (Country / State / County).**
  Schema is country-agnostic; expansion is data work, not schema work. See §12.2.
- **2026-05-22 — Map library: `react-leaflet` + Leaflet with OpenStreetMap tiles for Phase 2.**
  No API key, easy choropleths, smallest learning curve. Swap to MapLibre / vector tiles is a future option, not a blocker.
- **2026-05-22 — Time granularity is per-dataset, declared by an `IDatasetTimeProfile`.**
  Keeps the slider one component while supporting both season-level (Teams) and day-level (Games) data. See §12.4.
- **2026-05-22 — Admin UX is hybrid: generic tree editor + dedicated per-level pages.**
  Tree for ergonomic navigation and single-item edits; per-level pages for bulk import/export and high-volume grid editing. See §12.5.
- **2026-05-22 — Geometry stored twice: SQL Server `geography` columns (for server-side spatial queries) + cached GeoJSON/TopoJSON files (for frontend rendering).**
  Pays a small ingest-time cost for both forward-compatibility and runtime speed. Requires `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`. See §12.2.
- **2026-05-22 — Universal `AuditableEntity` base + `ICurrentUser` abstraction.**
  Every long-lived domain entity inherits `Id / IsActive / Source / CreatedAt / CreatedBy / UpdatedAt / UpdatedBy`. `CreatedBy/UpdatedBy` are email strings (denormalized) so audit history survives user deletion and is human-readable. `ICurrentUser` lives in Core so Infrastructure stays free of ASP.NET; the API layer provides `HttpContextCurrentUser`. See §5.
- **2026-05-22 — Soft-delete is the default; hard-delete is opt-in.**
  Services set `IsActive=false` instead of `Remove`. Avoids accidental data loss and preserves audit chains for historical analysis.
- **2026-05-22 — `Source` provenance field on every auditable row.**
  Free-form string (`"seed:bootstrap"`, `"seed:mlb-initial"`, `"import:nfl-2024"`, `"manual"`, etc.). Makes it trivial to identify, count, and bulk-re-process rows from a specific ingest run.
- **2026-05-23 — Geo seed source: us-atlas `counties-10m.geojson` (TopoJSON repo's GeoJSON export), embedded as a resource in `DataHub.Infrastructure`.**
  ~3.6 MB, ~3,143 features, WGS84 lon/lat, suitable for choropleth at national zoom. Embedded (not `CopyToOutputDirectory`) so it survives publishing. State polygons are *not* a separate file — they're computed by dissolving counties (see below). Feature `id` is the 5-digit county FIPS; first 2 digits → state FIPS → joined to a hard-coded `UsStates.ByFips` lookup for postal code + display name (territories like PR/VI omitted because the source doesn't include them).
- **2026-05-23 — Geometry insertion strategy: SQL Server is the orientation authority. EF Core inserts rows with `Geometry = NULL`; raw SQL then sets geometry via `geography::STGeomFromText(@wkt, 4326)`, with a per-polygon orientation-discovery retry for MultiPolygons whose sub-polygons disagree.**
  Background: NTS's `SqlServerBytesWriter` does a *planar* CCW test that systematically disagrees with SQL Server's *spherical* orientation rule for ~84% of US counties, and no client-side shoelace/orientation algorithm reliably matches what the server accepts. The robust approach is to let the server itself be the oracle: send WKT, catch orientation errors, and probe each sub-polygon in either orientation with a no-op `SELECT geography::STGeomFromText(...)` to find what the parser accepts, then rebuild a clean MultiPolygon WKT and UPDATE. Eliminates all county geometry failures.
- **2026-05-23 — State polygons computed in SQL via `geography::UnionAggregate(Geometry.MakeValid())` grouped by `StateId`, run after all counties are loaded.**
  NTS's `UnaryUnionOp` chokes on coordinates near the antimeridian (American Samoa territory bleed, Aleutians). SQL Server's planetary union handles these natively. `MakeValid()` on each input county defends against `GeographyUnionAggregate` rejecting any individual invalid geometry. Single SQL statement updates all 51 states.
- **2026-05-23 — Geo cache: per-entity GeoJSON files + per-parent bundles served as static assets from `wwwroot/geo-cache/`.**
  API DTOs intentionally omit raw geometry to keep responses small; the frontend reads geometry from these cache files instead. Layout: `countries/{id}.geojson`, `states/{id}.geojson`, `counties/{id}.geojson`, plus `states/bundle-{countryId}.geojson` and `counties/bundle-{stateId}.geojson`. Served with `Content-Type: application/geo+json` and `Cache-Control: public, max-age=31536000, immutable`. Cache root configurable via `Geo:CacheRoot`; defaults to `<contentRoot>/wwwroot/geo-cache`. Cache writer is invoked from `GeoService` on every create/update/setGeometry call.

---

## 10. TODO / Future

- [ ] Add `/api/auth/me` endpoint so the frontend can rehydrate the full user on silent refresh (currently the user object isn't restored after a hard refresh until next login).
- [ ] Roles & Permissions admin UI (CRUD + assignment).
- [ ] Data Sources & Data Entries CRUD UIs (currently placeholders).
- [ ] Background jobs / schedulers for periodic scraping & API pulls (e.g., Hangfire or Quartz.NET).
- [ ] Typed entities for first solid domain (likely market data) + migration from generic `DataEntries`.
- [ ] Query/analyze surface: saved queries, charts, ad-hoc SQL view.
- [ ] Audit log table (who changed what, when).
- [ ] Production hardening: HTTPS-only cookies, rotate JWT signing key, secrets via env vars / Key Vault.
- [ ] Rate limiting + request validation pipeline.
- [ ] Frontend: code-splitting (bundle is >500KB), centralized error handling, dark mode toggle.
- [ ] CI: build + test pipeline; pre-commit hooks.
- [ ] **Phase 2 — Sports domain + US Geo reference data + Map/Grid/Dashboard viewers + Universal Time Slider** (see §12 for full plan).

---

## 11. Quick Reference

### Run locally
```bash
# Terminal 1: API (auto-migrates + seeds)
dotnet run --project src/DataHub.Api

# Terminal 2: UI
cd src/datahub-ui && npm install && npm run dev
```
Open http://localhost:5173 → login → `tedlucas@outlook.com` / `DataZilla.247`.

### Create a migration
```bash
dotnet ef migrations add <Name> \
  --project src/DataHub.Infrastructure \
  --startup-project src/DataHub.Api \
  --output-dir Data/Migrations
```

### Apply migrations manually
```bash
dotnet ef database update \
  --project src/DataHub.Infrastructure \
  --startup-project src/DataHub.Api
```
(Not strictly needed — `Program.cs` migrates on startup.)

---

## 12. Phase 2 — Sports Domain, Map-Centric UI & Time-Aware Data

> **Status:** Design / planning. No code yet. This phase introduces the first concrete data domain (**Sports**), establishes a reusable **hierarchical-taxonomy pattern**, and defines the cross-cutting UX primitives every future domain will reuse: a **map-centric viewer**, a **grid viewer**, a **dashboard viewer**, and a **universal time slider**.

### 12.1 Domain Model — Sports Taxonomy

Sports data is organized as a strict hierarchy. Each level is its own entity so admins can manage them independently and so we can attach data, media, and metadata at any level.

```
Sports (root domain)
 └── Sport            e.g. Baseball, Football, Basketball, Hockey, Golf
      └── Level       e.g. Professional, Collegiate, High School
           └── League e.g. MLB, NFL, NCAA D-I, MHSAA
                └── Conference / Division   (optional, league-specific)
                     └── Team       e.g. Cardinals, Yankees
                          └── Season  (Team × Year)
                               └── Roster / Player / Game (future)
```

#### 12.1.1 Tables (proposed)

| Table | Key fields | Notes |
|-------|-----------|-------|
| `Sports` | Id (Guid), Name (unique), Slug, IconRef, SortOrder, IsActive, CreatedAt, UpdatedAt | Top-level (Baseball, Football, …) |
| `SportLevels` | Id, SportId (FK), Name, SortOrder, IsActive; UNIQUE(SportId, Name) | (Sport, Level) — e.g. Baseball/Professional |
| `Leagues` | Id, SportLevelId (FK), Name, Abbreviation, Country, FoundedYear, IsActive | MLB, NFL, NCAA D-I |
| `Conferences` | Id, LeagueId (FK), Name, ParentConferenceId (self-FK, nullable), IsActive | Supports Division-of-Conference |
| `Teams` | Id, LeagueId (FK), ConferenceId (FK, nullable), Name, City, State, Country, FoundedYear, PrimaryColor, SecondaryColor, LogoRef, VenueId (FK, nullable), IsActive | Joins to geography via City/State |
| `Venues` | Id, Name, Address, City, State, Country, Lat, Lon, Capacity, OpenedYear, ClosedYear, IsActive | Geolocated — drives map point overlays |
| `TeamSeasons` | Id, TeamId (FK), Year, LeagueId (FK), ConferenceId (FK, nullable), EffectiveFrom, EffectiveTo, Notes | Time-scoped team facts (e.g. league changes, relocations) |

**Why split Level out of Sport:** the same level name ("Professional") recurs across Sports, but the *teams* under Baseball/Professional are different from Football/Professional. Modeling Level as `(SportId, Name)` keeps the dropdowns sensible and lets us add a per-sport icon, color, or sort order.

**Why introduce League between Level and Team:** without it, "Cardinals" is ambiguous (St. Louis baseball vs. Arizona football vs. Louisville college). League pins it down and is the natural unit for schedules, standings, and rules variations later.

#### 12.1.2 Hierarchical-taxonomy pattern (reusable)

This Sport → Level → League → Conference → Team shape is a specific instance of a general pattern we will reuse for other domains (Geography below, future taxonomies). All taxonomy entities share:

- `Id` (Guid), `Name`, `Slug`, `SortOrder`, `CreatedAt`, `UpdatedAt`, `IsActive`
- A nullable `ParentId` or strongly-typed parent FK
- **Soft-delete** (`IsActive=false`) instead of hard-delete, so historical data references survive

#### 12.1.3 Time-awareness on every fact

Every fact-bearing row in the Sports module (and elsewhere) carries a **bitemporal time window**:

- `EffectiveFrom` (UTC, required)
- `EffectiveTo` (UTC, nullable — open-ended means "currently true")

This lets us render historical truth ("show the league as of 1998") without destructive edits. Examples:
- A Team that relocated has two `TeamSeasons` ranges in different cities.
- A League whose conference structure changed has overlapping `Conferences` rows with non-overlapping time windows.

---

### 12.2 Geographic Reference Data (US-only, Phase 2)

A separate taxonomy supports the map-centric UI. **Scope is intentionally US-only for Phase 2**; schema is country-agnostic so expansion is data work, not schema work.

| Table | Key fields | Notes |
|-------|-----------|-------|
| `Countries` | Id, IsoAlpha2 (unique), IsoAlpha3, Name, Geometry (`geography`, nullable), GeoJsonRef | Seeded with USA only for Phase 2 |
| `StatesProvinces` | Id, CountryId (FK), Code (e.g. "MO"), Name, Geometry (`geography`, nullable), GeoJsonRef | 50 US states + DC |
| `Counties` | Id, StateId (FK), FipsCode (unique), Name, Geometry (`geography`, nullable), GeoJsonRef | ~3,143 US counties; FIPS is the canonical join key |

**Dual geometry storage** (decision §9, 2026-05-22):

- **`Geometry` column** (SQL Server `geography`) — for future server-side spatial queries (contains-point, within-radius, intersect-with-polygon). Requires `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`.
- **`GeoJsonRef`** — relative path/URL to a pre-simplified GeoJSON/TopoJSON file served as a static asset for fast frontend rendering.

The frontend uses `GeoJsonRef`; the API may consult `Geometry` for spatial operations later.

#### 12.2.1 Geometry pipeline

| Step | Source | Tool / target |
|------|--------|---------------|
| Country (USA) | Natural Earth 1:50m admin-0 | Convert to GeoJSON, simplify to ~50KB |
| States (50 + DC) | US Census TIGER/Line (`tl_*_us_state.shp`) | Convert with `ogr2ogr`, simplify to ~50KB total |
| Counties (~3,143) | US Census TIGER/Line (`tl_*_us_county.shp`) | Convert, simplify per-state to ~5KB / county |

- Files land in `src/datahub-ui/public/geo/{country,state,county}/<id>.geojson` (or are served from `GET /api/geo/geometry/...` with an ETag).
- The same source data is imported into the DB `Geometry` columns by a one-time seed (or migration data step).
- Simplification target balances size and visual fidelity. Tune later.

#### 12.2.2 Joining sports to geography

- `Venues.Lat/Lon` → point overlays on the map.
- `Teams.State`, `Teams.City` → choropleth aggregation by state / county.
- All overlays respect the active time slider window (§12.4).

---

### 12.3 Frontend — Three Viewer Modes

Every dataset (Sports first, others to follow) is viewable through three interchangeable surfaces that share filters and the time slider:

| Mode | Purpose | Library |
|------|---------|---------|
| **Map** | *Primary* view. Choropleths + point overlays + drill-down. | `react-leaflet` + Leaflet, OpenStreetMap tiles |
| **Grid** | Power-user tabular browse, sort, filter, export. | MUI X DataGrid (Community) |
| **Dashboard** | Aggregated KPIs, charts, breakdowns. | Recharts (or Apache ECharts if needs grow) |

#### 12.3.1 Map drill-down

The map operates in four scopes, switchable via a control:

1. **World** — overview (Phase 2: only US is highlighted/clickable; rest of world is greyed)
2. **Country (US)** — US choropleth by state
3. **State** — single-state view, choropleth by county
4. **County** — single-county view, point-level data (venues, events)

Drill-down is bidirectional: click a state in Country view to enter State view; a breadcrumb (`USA › Missouri › St. Louis County`) is always visible and clickable.

#### 12.3.2 Layout sketch

```
┌──────────────────────────────────────────────────────────────┐
│ AppBar (existing)                                            │
├──────────┬───────────────────────────────────────────────────┤
│ Drawer   │ Filters bar (Sport, Level, League, …)             │
│ (nav)    ├───────────────────────────────────────────────────┤
│          │ View toggle: [ Map | Grid | Dashboard ]           │
│          ├───────────────────────────────────────────────────┤
│          │                                                   │
│          │     ACTIVE VIEWER (Map / Grid / Dashboard)        │
│          │                                                   │
│          ├───────────────────────────────────────────────────┤
│          │ Time slider:  [1900 ●━━━━━●━━━━━━━━━━ 2026]       │
│          │ Range: 1985-01-01 → 2010-12-31  [Play ▶] [step]   │
│          └───────────────────────────────────────────────────┘
└──────────────────────────────────────────────────────────────┘
```

---

### 12.4 Universal Time Slider

A first-class, app-wide UI primitive:

- **Dual-handle range slider** with start and end dates.
- **Per-dataset granularity** (decision §9, 2026-05-22): each dataset declares an `IDatasetTimeProfile { granularity: 'day' | 'month' | 'year' | 'season'; minDate; maxDate; defaultStep; snapPoints? }`. The slider adapts tick marks, snap behavior, and playback step to the active profile.
- **Playback** mode animates the upper handle forward at a configurable step (e.g., 1 year/sec for season-level data, 1 day/sec for game-level data).
- **Snap** to common boundaries (season start, calendar year, decade) when the profile defines `snapPoints`.
- State lives in a React context (`TimeRangeContext`) so Map / Grid / Dashboard all react to the same window.
- **URL-synced** (`?from=…&to=…&g=year`) so views are shareable/bookmarkable.

---

### 12.5 Admin UI — Hybrid Hierarchical Editor

Decision §9 (2026-05-22): we offer **two complementary surfaces** that hit the same API and respect the same permissions.

#### 12.5.1 Generic Tree Editor — `/admin/taxonomy/sports`

- **Left pane:** lazy-loaded tree of the full hierarchy (Sport → Level → League → … → Team).
- **Right pane:** form for the selected node, with **+ Add child** action contextual to the selected level.
- Permission-gated: `sports:read` to view, `sports:manage` to mutate.
- The component is **generic over the taxonomy** — driven by a per-domain schema descriptor (level names, allowed children, form fields). Reused later by `/admin/taxonomy/geography`.

#### 12.5.2 Dedicated per-level pages

For high-volume operations the tree is bad at:

| Route | Purpose |
|-------|---------|
| `/admin/sports` | List + create Sports |
| `/admin/sport-levels` | List + create Levels (filtered by Sport) |
| `/admin/leagues` | List + create Leagues (filtered by Sport/Level), **CSV/JSON bulk import** |
| `/admin/conferences` | List + create Conferences (filtered by League) |
| `/admin/teams` | DataGrid view of Teams with inline edit + **CSV/JSON bulk import/export** (e.g., seed all 30 MLB teams in one shot) |
| `/admin/venues` | List + create + import |

Each page uses the same MUI X DataGrid component pattern; bulk import accepts CSV or JSON, validates row-by-row, and shows a per-row success/error report.

#### 12.5.3 Soft-delete

All taxonomy CRUD uses soft-delete (toggle `IsActive`), with a "show inactive" filter. Hard-delete is admin-only and prohibited if any time-scoped fact references the row.

---

### 12.6 New Permissions (to be seeded)

- `sports:read`, `sports:manage`
- `geo:read`, `geo:manage`

These follow the same pattern as existing permissions: add to `DataHub.Core.Constants.Permissions`, include in `Permissions.All`, and the seeder + policy registration handle the rest (see §8 "Adding a new permission").

---

### 12.7 New API Endpoints (sketch — to be detailed when implemented)

```
# Sports taxonomy CRUD
GET    /api/sports                              (sports:read)
POST   /api/sports                              (sports:manage)
GET    /api/sports/{sportId}/levels             (sports:read)
POST   /api/sports/{sportId}/levels             (sports:manage)
GET    /api/sport-levels/{levelId}/leagues      (sports:read)
POST   /api/sport-levels/{levelId}/leagues      (sports:manage)
GET    /api/leagues/{leagueId}/conferences      (sports:read)
POST   /api/leagues/{leagueId}/conferences      (sports:manage)
GET    /api/leagues/{leagueId}/teams            (sports:read)
POST   /api/leagues/{leagueId}/teams            (sports:manage)
GET    /api/teams/{teamId}                      (sports:read)

# Bulk import (per level)
POST   /api/sports/{sportId}/levels/import      (sports:manage)
POST   /api/leagues/import                      (sports:manage)
POST   /api/teams/import                        (sports:manage)
POST   /api/venues/import                       (sports:manage)

# Query (Map / Grid / Dashboard)
GET    /api/teams?sport=baseball&level=professional&state=MO&from=…&to=…   (sports:read)
GET    /api/venues?bbox=…&from=…&to=…                                       (sports:read)

# Geography
GET    /api/geo/countries                       (geo:read)
GET    /api/geo/states?country=US               (geo:read)
GET    /api/geo/counties?state=MO               (geo:read)
GET    /api/geo/geometry/{level}/{id}           (geo:read)  -- returns simplified GeoJSON, ETag-cached
```

All collection endpoints accept `from` / `to` query params honoring the active time window. All write endpoints validate that parent FKs exist and that `(SportId, Name)`-style uniqueness holds.

---

### 12.8 Build Order (suggested, not binding)

1. **Backend taxonomy:** `Sports`, `SportLevels`, `Leagues`, `Conferences`, `Teams`, `Venues` entities + EF configurations + migration + CRUD endpoints + new permissions in seeder.
2. **Backend geography:** `Countries`, `StatesProvinces`, `Counties` entities with both `Geometry` (NetTopologySuite) and `GeoJsonRef`; one-time seed that imports US data and emits GeoJSON files to `src/datahub-ui/public/geo/`.
3. **Frontend — generic tree editor (`TaxonomyAdmin`)** wired to Sports first.
4. **Frontend — `TimeRangeContext` + slider primitive** with a baked-in `IDatasetTimeProfile` for Teams (year-level).
5. **Frontend — Grid viewer** (lowest risk, fastest payoff, validates the filter + time-window contract).
6. **Frontend — Map viewer** at Country scope, then State, then County (drill-down).
7. **Frontend — Dashboard viewer** with a small set of starter charts (teams per state, leagues per sport over time, venue capacity distribution).
8. **Frontend — per-level admin pages** with bulk CSV/JSON import/export.
9. **Cross-cutting:** URL sync (`?from&to&g&scope`), drill-down breadcrumb, playback animation.

---

### 12.9 Open Questions / Deferred

- **Vector tiles?** If raster OSM tiles feel limiting, evaluate MapLibre GL + a vector tile provider. Not blocking Phase 2.
- **Spatial queries on the server.** We're storing `geography` columns now but not using them in Phase 2. First real use case (e.g. "all venues within 100mi of a point") will validate the choice.
- **Real-time data.** Sports schedules and live scores are out of scope for Phase 2; the architecture supports them (TeamSeasons → Games → LiveEvents) but we build that when there's a concrete need.
- **Non-US geography.** Schema supports it; we defer the data + UX work until a non-US dataset arrives.

