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

`users:read`, `users:manage`, `roles:manage`, `data:read`, `data:write`, `sources:manage`

### Seeded data (idempotent, on every startup)

- All default permissions
- `Admin` role with **every** permission attached
- Admin user `tedlucas@outlook.com` / `DataZilla.247` (hashed with ASP.NET Identity `PasswordHasher`)

> **Note:** Self-registration is disabled. New users must be created by an admin via `POST /api/users`.

---

## 5. Data Model

### Auth tables

| Table | Key fields |
|-------|-----------|
| `Users` | Id (Guid), Email (unique), PasswordHash, FirstName, LastName, IsActive, CreatedAt, UpdatedAt |
| `Roles` | Id, Name (unique), Description |
| `Permissions` | Id, Name (unique), Description |
| `UserRoles` | (UserId, RoleId) composite PK |
| `RolePermissions` | (RoleId, PermissionId) composite PK |
| `RefreshTokens` | Id, UserId, Token (unique), ExpiresAt, CreatedAt, RevokedAt |

### Data tables (flexible-first)

| Table | Key fields |
|-------|-----------|
| `DataSources` | Id, Name, Type, Description, ConfigJson (nvarchar(max)), CreatedAt |
| `DataEntries` | Id, DataSourceId (FK, nullable), Category, Tags (csv), PayloadJson (nvarchar(max)), CreatedAt, CreatedByUserId (FK, nullable) |

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
- **Entities** use `Guid` primary keys, auto-defaulted via property initializers.
- **JSON columns** are typed as `nvarchar(max)` and named with `Json` suffix (e.g., `PayloadJson`).
- **Permissions** are referenced via the strongly-typed `DataHub.Core.Constants.Permissions` constants — never hard-code permission strings in controllers.
- **DTOs are `record`** types in `DataHub.Core.DTOs.*` namespaces.
- **Services** are registered in `InfrastructureServiceRegistration.AddDataHubInfrastructure`.
- **DbContext** auto-discovers `IEntityTypeConfiguration<T>` from the Infrastructure assembly.
- **Auth in controllers**: prefer `[Authorize(Policy = Permissions.SomePermission)]` over role checks.

### Adding a new entity
1. Add the entity class under `DataHub.Core/Entities/`.
2. Add an `IEntityTypeConfiguration<T>` in `DataHub.Infrastructure/Data/Configurations/EntityConfigurations.cs`.
3. Add a `DbSet<T>` in `DataHubDbContext`.
4. Create a migration: `dotnet ef migrations add <Name> -p src/DataHub.Infrastructure -s src/DataHub.Api -o Data/Migrations`.
5. Add DTOs in `DataHub.Core/DTOs/`, a service interface in `Core/Interfaces/`, an implementation in `Infrastructure/Services/`, and a controller in `Api/Controllers/`.
6. Update this doc's API Endpoints section.

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
