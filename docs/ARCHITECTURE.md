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
- `GET    /api/geo/countries/{countryId}/states`              list states in a country (by id)
- `GET    /api/geo/states?country={iso2}`                     list states in a country (by ISO-2, used by the map UI)
- `POST   /api/geo/countries/{countryId}/states`              create state under a country
- `GET    /api/geo/states/{id}`, `PUT`, `DELETE`
- `GET    /api/geo/states/{stateId}/counties`                 list counties in a state (by id)
- `GET    /api/geo/counties?state={fips}`                     list counties in a state (by state FIPS, used by the map UI)
- `POST   /api/geo/states/{stateId}/counties`                 create county under a state
- `GET    /api/geo/counties/{id}`, `PUT`, `DELETE`
- `GET    /api/geo/metrics?level={country|state|county}&parent={iso2|fips}&metric={regions|teams|venues}`   choropleth feed: `[{ fips, name, count }]`. `metric` defaults to `regions` (counts of geographic children); `teams` / `venues` aggregate the Sports domain via `Team.State` / `Venue.State` postal → FIPS lookup. Teams/Venues fall back to `regions` at county level (those entities don't store a county yet).

Geometry is **not** stored in the database (see §12.2). The map UI loads boundary geometry from the static asset endpoints below and joins it to `/api/geo/metrics` by FIPS / ISO-2.

#### Geo static assets (no auth)
Served by ASP.NET Core static-file middleware from `wwwroot/geo/` with `Cache-Control: public, max-age=31536000, immutable` and `Content-Type: application/geo+json` for `.geojson`.
```
/geo/countries-110m.topo.json   world-atlas countries TopoJSON (~108 KB)
/geo/us-states-10m.topo.json    us-atlas US states TopoJSON (~115 KB)
/geo/us-counties-10m.topo.json  us-atlas US counties TopoJSON (~842 KB)
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
- **2026-05-24 — Geo module radically simplified: NTS, `geography` columns, the per-entity cache writer, `set-geometry` endpoints, and the seed-time county→state polygon dissolve are all removed at runtime.** (Supersedes the dual-storage decision of 2026-05-22 and every geometry-pipeline decision of 2026-05-23.)
  Rationale: the previous design solved problems we don't have yet (server-side spatial queries) at the cost of one of the trickiest ingest pipelines in the codebase (SQL Server vs. NTS orientation drift, antimeridian unions, raw-SQL fallbacks). The map only ever needed *boundaries* (which never change) and *metrics* (which do). So we ship the boundaries as immutable static files under `wwwroot/geo/` (world-atlas + us-atlas, ~3.8 MB total), expose a single `GET /api/geo/metrics?level=...&parent=...` choropleth feed, and join them client-side by FIPS / ISO-2. The `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` NuGet stays in the csproj only because the original `AddGeoModule` migration's Designer file references `NetTopologySuite.Geometries.Geometry`; the runtime code path no longer touches NTS. A new `DropGeoGeometryColumns` migration removes the three `geography` columns from existing databases.
- **2026-05-24 — Map library reversed: `maplibre-gl` + `react-map-gl` + `topojson-client` instead of `react-leaflet` + Leaflet.** (Supersedes the map-library decision of 2026-05-22.)
  MapLibre gives us data-driven styling (`interpolate` expressions on a `metric` feature property), zoom-driven layer LOD (per-layer `minzoom`/`maxzoom`), and feature-state hover without a re-render — all things we were going to fight Leaflet for once the choropleth grew beyond trivial. We deliberately ship *no* external basemap (no Mapbox / MapTiler / OSM tile server, no API keys, no rate limits): the style is just a flat background plus three boundary layers, which is exactly what a choropleth-first product needs. A real raster basemap can be dropped in later by adding one `raster` source to `mapStyle.ts`.
- **2026-05-24 — `/api/geo/metrics` is the single choropleth feed; what's counted is selected by a `metric` query param (`regions` | `teams` | `venues`), not by separate endpoints.**
  Keeps the frontend join logic identical across metrics — fetch a flat `[{ fips, name, count }]`, merge by `joinKey`, let MapLibre's data-driven `fill-color` repaint. The state-level join for Teams/Venues relies on the seeded `UsStates.FipsByPostal` reverse lookup (postal "MO" → FIPS "29"), so we promoted that table from `internal` to `public`. Country-level joins for Teams/Venues normalize ISO-3 ("USA") → ISO-2 ("US") via the Countries table so the choropleth doesn't double-count countries that show up in two encodings. Teams and Venues currently have no county column; calling them at county level transparently falls back to the `regions` placeholder rather than returning an empty FeatureCollection. New metrics (e.g. `events`, `players`) add a single switch arm — boundaries, controller, and frontend picker need zero changes.
- **2026-05-24 — Generic `TaxonomyAdmin` component instead of one-off hierarchy editors per domain.**
  A `TaxonomySchema` descriptor (in `features/taxonomy/types.ts`) parameterizes a single Tree + Detail-form pair over any strictly hierarchical editable dataset. Sports is the first consumer; Geography will reuse the exact same component once its admin slice lands. Two shape-realities forced the descriptor to be richer than the obvious "level → child level" mapping: (a) a level can declare **multiple child groupings** (League has both Conferences and Teams (unassigned), each loading via its own filter), and (b) a level can declare itself as a child (Conference's children include Conferences). Pushing both into the descriptor — rather than special-casing them in the renderer — is what keeps the component reusable.
- **2026-05-25 — Antimeridian-splitting added to country boundary loading (`features/map/useGeoData.ts`).**
  world-atlas `countries-110m.topo.json` encodes Fiji, Russia, and Antarctica with rings whose decoded longitudes span ~360° (they include vertices on both sides of the ±180° seam). MapLibre's earcut triangulator sees a single ring 360° wide and produces nonsense triangles spanning the entire canvas — visible as long horizontal/diagonal bands across the world map. The fix is a small inline splitter (`splitRingAtAntimeridian`) that runs after `topojson.feature()`: for true antimeridian crossers (Fiji, Russia) it unwraps the ring into a continuous longitude run and Sutherland–Hodgman-clips it into east/west halves at lon=180; for polar rings (Antarctica), where there's no actual crossing and the 360° "edge" is the closure running along the antimeridian, it routes the closure through the south/north pole instead — geographically zero-length, Cartesian-wise a clean trapezoidal flap. No dependency added (turf/d3-geo would have worked but this is ~70 lines and the world-atlas surface area is fixed).
- **2026-05-25 — Time is app-wide state, not per-page. `TimeRangeProvider` wraps the entire authenticated app; viewers register a `IDatasetTimeProfile` via `useRegisterTimeProfile()` on mount and read the window via `useTimeRange()`. The fixed-footer `TimeBar` is present on every page so time-awareness reads as a property of the *application*, not of individual viewers.**
  Rationale: forcing every viewer to render its own slider would (a) duplicate UI, (b) make cross-viewer comparison ("same window, different visualization") impossible without manual sync, (c) leak the URL-sync logic into every page, and (d) make playback a per-page feature rather than the global animation it ought to be. The price is a tiny bit of indirection (viewers must remember to register a profile), but `useRegisterTimeProfile` makes that a one-liner.
- **2026-05-25 — Frontend ships time-aware request shape now; backend filtering deferred until the Grid viewer needs it.**
  The Map already passes `?from&to&g` on every `/api/geo/metrics` call, but the backend currently ignores them and returns all-time metrics. Doing it this way means (a) the slider's contract is real today (URL-sharable, playback-animatable, etc.), and (b) when backend filtering lands it's purely an additive backend change — no frontend refactor. The cost is a brief period where the slider looks like it should affect the Map but doesn't; acceptable because the visible affordances (granularity picker, playback, profile picker) all *do* work, and the actual metric values just don't change with the window yet.
- **2026-05-25 — Granularity strategy pattern instead of `switch (granularity)` everywhere. Each `GranularityId` (`day`/`month`/`year`/`season`) has a `GranularityStrategy` object with `floor/ceil/step/count/format/tickStride`; the slider, context, and any future consumer call methods on the strategy, never branch on the id. Season is the US-sports `Sep–Aug` convention; reparameterizing for soccer / academic / fiscal years is a constant change.**
- **2026-05-24 — First Grid viewer (`/sports/mlb`) is league-specific, not generic; `Team` gets a `ClosedYear` column to make active-during-window filtering correct.**
  Two questions answered together. (a) **Scope:** a generic `/sports/leagues/:slug` page would be designing in the dark with exactly one league seeded — we'd guess at column sets, league-specific affordances (e.g. AL/NL → divisions), and detail panes for sports we don't have. The cheaper path is to ship a concrete MLB page, treat it as the reference impl, and factor out a generic viewer when the *second* league lands and the shared shape is observable rather than imagined. (b) **`Team.ClosedYear`:** the existing schema only had `FoundedYear`, which made the §12.1.3 active-during filter half-correct (`FoundedYear ≤ windowEnd` ✓, but no closed-side bound). Adding the nullable column is one migration and makes the filter symmetric — defunct teams (e.g. Montreal Expos) will fall out of windows after their close year, which is the user-facing point of having a time slider. The backend converts the slider's epoch-ms boundaries to calendar years before filtering (Team data is year-grained); the `g` query param is accepted and ignored so the slider URL contract still round-trips cleanly.

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
| `Countries` | Id, IsoAlpha2 (unique), IsoAlpha3, Name | Seeded with USA only for Phase 2 |
| `StatesProvinces` | Id, CountryId (FK), Code (e.g. "MO"), Name, Fips (unique-ish, 2-digit) | 50 US states + DC; FIPS is the canonical join key to static boundary files |
| `Counties` | Id, StateId (FK), Fips (unique, 5-digit), Name | ~3,143 US counties; FIPS joins to static boundary files and to choropleth metrics |

**Boundary geometry is not in the database** (revised 2026-05-24, supersedes the dual-storage decision of 2026-05-22). Boundaries are shipped as static asset files under `wwwroot/geo/`:

| File | Format | Source | Size |
|------|--------|--------|------|
| `countries-110m.topo.json` | TopoJSON | world-atlas | ~108 KB |
| `us-states-10m.topo.json`  | TopoJSON | us-atlas | ~115 KB |
| `us-counties-10m.topo.json` | TopoJSON | us-atlas | ~842 KB |

Served by ASP.NET Core static-file middleware with `Cache-Control: public, max-age=31536000, immutable` and `Content-Type: application/geo+json` for `.geojson`. The frontend (`src/datahub-ui/src/features/map/`) loads them via `fetch` + `topojson-client`, then joins to `/api/geo/metrics` by FIPS / ISO-2.

#### 12.2.1 How the join works

- **Country level** — joins on UN M49 numeric code (the `id` field in world-atlas) or ISO-2 once countries beyond the US are introduced.
- **State level** — joins on 2-digit state FIPS (`States.Fips` ↔ us-atlas state `id`).
- **County level** — joins on 5-digit county FIPS (`Counties.Fips` ↔ us-atlas county `id`).

This keeps the API DTOs tiny (no geometry payload), keeps geometry assets cacheable forever (boundaries don't change), and lets the choropleth recolor instantly when metrics change.

#### 12.2.2 Joining sports to geography

- `Venues.Lat/Lon` → point overlays on the map.
- `Teams.State`, `Teams.City` → choropleth aggregation by state / county via FIPS lookup.
- All overlays respect the active time slider window (§12.4).

#### 12.2.3 International expansion

When non-US data lands, drop a new static file into `wwwroot/geo/` (e.g. `ca-provinces.geojson`), extend the `LEVELS` config in `src/datahub-ui/src/features/map/layers.ts`, and ensure the relevant `Country` row exists with the right ISO codes. No schema change, no migration.

---

### 12.3 Frontend — Three Viewer Modes

Every dataset (Sports first, others to follow) is viewable through three interchangeable surfaces that share filters and the time slider:

| Mode | Purpose | Library |
|------|---------|---------|
| **Map** | *Primary* view. Choropleths + point overlays + zoom-driven drill-down. | `maplibre-gl` + `react-map-gl` + `topojson-client`; no external basemap by default |
| **Grid** | Power-user tabular browse, sort, filter, export. | MUI X DataGrid (Community) |
| **Dashboard** | Aggregated KPIs, charts, breakdowns. | Recharts (or Apache ECharts if needs grow) |

#### 12.3.1 Map drill-down

Drill-down is **zoom-driven** rather than mode-switched: each boundary level (Country / State / County) is a separate MapLibre layer with `minzoom`/`maxzoom` bounds (see `src/datahub-ui/src/features/map/layers.ts`). As the user zooms in, the layer swap happens automatically; clicking a feature flies the camera into that level's typical view zoom.

Conceptual scopes:

1. **World** — country choropleth (zoom 0–4)
2. **Country (US)** — state choropleth (zoom 3–7)
3. **State** — county choropleth (zoom 6+)
4. **County** — point-level overlays (venues, events) at the deepest zoom

A breadcrumb (`USA › Missouri › St. Louis County`) is rendered alongside the map and reflects whatever feature is hovered or last clicked; clicking a breadcrumb crumb flies back out to that level.

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

### 12.4 Universal Time Slider ✅ implemented 2026-05-25 (frontend; backend filtering deferred)

A first-class, app-wide UI primitive living in `src/datahub-ui/src/features/time/`:

- **Dual-handle range slider** with start and end dates (MUI `Slider` in range mode, operating in bucket-index space).
- **Four granularities shipped:** `day`, `month`, `year`, `season`. Each is a `GranularityStrategy` (`granularity.ts`) implementing `floor / ceil / step / count / format / tickStride`. Season = US-sports year, Sep YYYY → Aug YYYY+1, formatted `YYYY-YY`. Picker in the footer switches between them; the slider re-buckets without losing the active window (just re-snaps it to the new granularity's boundaries).
- **`IDatasetTimeProfile`** (`types.ts`): viewer pages declare `{ id, label, granularity, minDate, maxDate, defaultStep, snapPoints? }`. Pages register via `useRegisterTimeProfile(profile)`; the footer's profile picker only appears when >1 profile is registered. The TimeBar renders even when zero profiles are registered (a fallback "All time" profile keeps the bar visible, disabled, so layout is constant across pages).
- **Playback** (`expand` | `slide` mode, 0.5–10× speed): `requestAnimationFrame` loop in the provider advances the upper handle by `defaultStep × speed` buckets per second, optionally dragging the lower handle in lockstep (`slide`). Auto-stops when the upper handle reaches the profile's `maxDate`.
- **State lives in `TimeRangeContext`** (`TimeRangeContext.tsx`) wrapped around the entire authenticated app in `App.tsx`. Map / Grid / Dashboard all read from `useTimeRange()`.
- **URL-synced** (`?from=…&to=…&g=…&profile=…`) via `history.replaceState` on every change; hydrated on mount. No history pollution, back/forward unaffected.
- **Filter semantics: active-during-window (overlap).** Per §12.1.3 contract — a fact matches the window iff `[EffectiveFrom, EffectiveTo] ∩ [from, to] ≠ ∅`. This is the contract the backend will honor when filtering lands; the frontend already passes `from`/`to`/`g` on every metrics request.
- **Footer placement:** `TimeBar` is a fixed-bottom `Paper` (z-index above the sidebar drawer). Layout adds bottom padding equal to the bar's height so content never hides behind it.
- **Map wiring:** `MapPage` registers `sports.regions` (1850 → next-year, year granularity, step 1) and forwards the active window to `<MapView time={…}>`. `useGeoMetrics.fetchMetrics` includes `from`/`to`/`g` in the `/api/geo/metrics` query. The backend currently ignores those params — it'll start honoring them when the Grid viewer needs server-side filtering (deferred from this slice on purpose).

Open follow-ups (tracked in §10):
- Backend `from`/`to`/`g` honoring on `/api/geo/metrics` (and the Sports queries the Grid will use).
- `snapPoints` on the slider (no consumer needs it yet — current snapping is granularity-aligned).
- Per-bucket histogram overlay on the slider track (à la Kibana) once we have row counts to plot.

---

### 12.5 Admin UI — Hybrid Hierarchical Editor

Decision §9 (2026-05-22): we offer **two complementary surfaces** that hit the same API and respect the same permissions.

#### 12.5.1 Generic Tree Editor — `/admin/taxonomy/sports` ✅ implemented 2026-05-24

- **Left pane (`features/taxonomy/TaxonomyTree.tsx`):** lazy-loaded tree. Each node renders its children grouped into named sub-folders (one per `ChildGrouping` declared by the level descriptor); each grouping loads on first expand and caches its result. The chevron toggles the node; clicking the row selects it.
- **Right pane (`TaxonomyAdmin.tsx`):** schema-driven form (`TaxonomyForm.tsx`) for the selected node, with Save / Delete actions. A modal `New <Singular>` dialog reuses the same form for creation.
- Permission-gated: `sports:read` to view, `sports:manage` for the mutate buttons (Add / Save / Delete are disabled but the tree remains browsable).
- **Generic over the taxonomy.** A `TaxonomySchema` (`features/taxonomy/types.ts`) is a `Record<levelId, LevelDescriptor>` where each level declares `singular`/`plural`, a `fields[]` form descriptor, an ordered list of `ChildGrouping`s, and async `create`/`update`/`remove` callbacks. The same component will host Geography (`Country → State → County`) by supplying a second schema.
- Two real-world wrinkles are first-class in the model rather than special-cased in the renderer:
  - **Multiple child groupings under one level.** A League's children are *Conferences* and *Teams (unassigned)*. Each grouping is its own folder with its own loader filter (`!t.conferenceId` for unassigned teams).
  - **Self-recursive levels.** Conference declares two child groupings — *Sub-conferences* (recursive) and *Teams*. Both reach for the League ancestor via a small `ancestorOfLevel(node, 'league')` walk so they can call `/api/leagues/{leagueId}/{conferences|teams}` and filter by `parentConferenceId` / `conferenceId`.
- Venues are **not** in the Sports tree (they're flat, referenced by `Team.VenueId`); they'll get a dedicated admin page in step 8.
- Soft-delete is exposed as an `Active` checkbox on every level's form; the tree always lists with `includeInactive=true`.

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
GET    /api/geo/counties?state=06               (geo:read)   -- state FIPS
GET    /api/geo/metrics?level=...&parent=...    (geo:read)   -- choropleth feed
GET    /geo/{file}                              (no auth)    -- static boundary files
```

All collection endpoints accept `from` / `to` query params honoring the active time window. All write endpoints validate that parent FKs exist and that `(SportId, Name)`-style uniqueness holds.

---

### 12.8 Build Order (suggested, not binding)

1. **Backend taxonomy:** `Sports`, `SportLevels`, `Leagues`, `Conferences`, `Teams`, `Venues` entities + EF configurations + migration + CRUD endpoints + new permissions in seeder.
2. **Backend geography:** `Countries`, `StatesProvinces`, `Counties` entities (FIPS-keyed, no geometry column); seeder reads embedded us-atlas counties GeoJSON for `Counties` + `States` rows. Boundary files served as static assets from `wwwroot/geo/`.
3. **Frontend — generic tree editor (`TaxonomyAdmin`)** wired to Sports first. ✅ done 2026-05-24
4. **Frontend — `TimeRangeContext` + slider primitive** with a baked-in `IDatasetTimeProfile` for Teams (year-level). ✅ done 2026-05-25 (frontend; backend filtering deferred)
5. **Frontend — Grid viewer** (lowest risk, fastest payoff, validates the filter + time-window contract). ✅ first instance done 2026-05-24 — `/sports/mlb` (MlbTeams page) hardcoded to MLB; uses MUI `x-data-grid`; backend `GET /api/teams` now honors `from`/`to` with active-during-window semantics (§12.1.3) by translating epoch-ms → calendar years (`g` accepted but ignored — year resolution suffices for the Team entity).
6. **Frontend — Map viewer** at Country scope, then State, then County (drill-down).
7. **Frontend — Dashboard viewer** with a small set of starter charts (teams per state, leagues per sport over time, venue capacity distribution).
8. **Frontend — per-level admin pages** with bulk CSV/JSON import/export.
9. **Cross-cutting:** URL sync (`?from&to&g&scope`), drill-down breadcrumb, playback animation.

---

### 12.9 Open Questions / Deferred

- **Vector tile basemap.** The current style ships no basemap. If/when context (roads, hillshade, labels) becomes useful, add a `raster` or `vector` source to `mapStyle.ts` pointing at a tile provider; the boundary layers stack cleanly on top.
- **Spatial queries on the server.** We no longer store `geography` columns. The first real use case (e.g. "all venues within 100mi of a point") will revisit the decision — likely by reintroducing a single `geography` column on `Venues` rather than reviving the country/state/county geometry pipeline.
- **Real-time data.** Sports schedules and live scores are out of scope for Phase 2; the architecture supports them (TeamSeasons → Games → LiveEvents) but we build that when there's a concrete need.
- **Non-US geography.** Schema supports it; we defer the data + UX work until a non-US dataset arrives.

