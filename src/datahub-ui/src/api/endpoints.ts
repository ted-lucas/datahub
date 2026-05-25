import { api } from './client'

export interface UserDto {
  id: string
  email: string
  firstName: string
  lastName: string
  isActive: boolean
  roles: string[]
  permissions: string[]
}

export interface LoginResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: UserDto
}

export const authApi = {
  login: (email: string, password: string) =>
    api.post<LoginResponse>('/auth/login', { email, password }).then((r) => r.data),
  refresh: () =>
    api.post<{ accessToken: string; accessTokenExpiresAt: string }>('/auth/refresh').then((r) => r.data),
  logout: () => api.post('/auth/logout'),
}

export const usersApi = {
  list: () => api.get<UserDto[]>('/users').then((r) => r.data),
}

// ── Geo ────────────────────────────────────────────────────────────────────
// Boundary geometries are served as static files under `/geo/*` (see
// `features/map/useGeoData.ts`). The endpoints below cover the read-only
// catalog + the choropleth metrics feed.

export interface CountryDto {
  id: string
  iso2: string
  iso3: string
  name: string
}

export interface StateDto {
  id: string
  countryId: string
  fips: string
  name: string
  abbreviation: string
}

export interface CountyDto {
  id: string
  stateId: string
  fips: string
  name: string
}

export interface GeoMetricDto {
  fips: string
  name: string
  count: number
}

export type GeoMetricsLevel = 'country' | 'state' | 'county'
/** Matches DataHub.Core.Interfaces.GeoMetricKind. */
export type GeoMetricKind = 'regions' | 'teams' | 'venues'

export const geoApi = {
  listCountries: () => api.get<CountryDto[]>('/geo/countries').then((r) => r.data),
  listStates: (countryIso2: string) =>
    api.get<StateDto[]>('/geo/states', { params: { country: countryIso2 } }).then((r) => r.data),
  listCounties: (stateFips: string) =>
    api.get<CountyDto[]>('/geo/counties', { params: { state: stateFips } }).then((r) => r.data),
  metrics: (level: GeoMetricsLevel, parent?: string, metric: GeoMetricKind = 'regions') =>
    api
      .get<GeoMetricDto[]>('/geo/metrics', {
        params: { level, metric, ...(parent ? { parent } : {}) },
      })
      .then((r) => r.data),
}
