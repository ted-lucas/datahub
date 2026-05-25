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

// ── Sports ────────────────────────────────────────────────────────────────
// Mirrors DataHub.Core.DTOs.Sports. The shape is a strict hierarchy
//   Sport → SportLevel → League → Conference (self-recursive) → Team
// with Venue as a flat sibling list referenced by Team.VenueId.

export interface SportDto {
  id: string
  name: string
  slug: string
  iconRef: string | null
  sortOrder: number
  isActive: boolean
}
export interface CreateSportRequest {
  name: string
  slug: string
  iconRef: string | null
  sortOrder: number
}
export interface UpdateSportRequest extends CreateSportRequest { isActive: boolean }

export interface SportLevelDto {
  id: string
  sportId: string
  name: string
  sortOrder: number
  isActive: boolean
}
export interface CreateSportLevelRequest { name: string; sortOrder: number }
export interface UpdateSportLevelRequest extends CreateSportLevelRequest { isActive: boolean }

export interface LeagueDto {
  id: string
  sportLevelId: string
  name: string
  abbreviation: string | null
  country: string | null
  foundedYear: number | null
  isActive: boolean
}
export interface CreateLeagueRequest {
  name: string
  abbreviation: string | null
  country: string | null
  foundedYear: number | null
}
export interface UpdateLeagueRequest extends CreateLeagueRequest { isActive: boolean }

export interface ConferenceDto {
  id: string
  leagueId: string
  parentConferenceId: string | null
  name: string
  isActive: boolean
}
export interface CreateConferenceRequest {
  name: string
  parentConferenceId: string | null
}
export interface UpdateConferenceRequest extends CreateConferenceRequest { isActive: boolean }

export interface TeamDto {
  id: string
  leagueId: string
  conferenceId: string | null
  venueId: string | null
  name: string
  city: string | null
  state: string | null
  country: string | null
  foundedYear: number | null
  primaryColor: string | null
  secondaryColor: string | null
  logoRef: string | null
  isActive: boolean
}
export interface CreateTeamRequest {
  name: string
  conferenceId: string | null
  venueId: string | null
  city: string | null
  state: string | null
  country: string | null
  foundedYear: number | null
  primaryColor: string | null
  secondaryColor: string | null
  logoRef: string | null
}
export interface UpdateTeamRequest extends CreateTeamRequest { isActive: boolean }

export const sportsApi = {
  // Sports
  listSports: (includeInactive = false) =>
    api.get<SportDto[]>('/sports', { params: { includeInactive } }).then((r) => r.data),
  createSport: (req: CreateSportRequest) =>
    api.post<SportDto>('/sports', req).then((r) => r.data),
  updateSport: (id: string, req: UpdateSportRequest) =>
    api.put<SportDto>(`/sports/${id}`, req).then((r) => r.data),
  deleteSport: (id: string) => api.delete(`/sports/${id}`).then(() => undefined),

  // Levels
  listLevels: (sportId: string, includeInactive = false) =>
    api.get<SportLevelDto[]>(`/sports/${sportId}/levels`, { params: { includeInactive } }).then((r) => r.data),
  createLevel: (sportId: string, req: CreateSportLevelRequest) =>
    api.post<SportLevelDto>(`/sports/${sportId}/levels`, req).then((r) => r.data),
  updateLevel: (id: string, req: UpdateSportLevelRequest) =>
    api.put<SportLevelDto>(`/sport-levels/${id}`, req).then((r) => r.data),
  deleteLevel: (id: string) => api.delete(`/sport-levels/${id}`).then(() => undefined),

  // Leagues
  listLeagues: (sportLevelId: string, includeInactive = false) =>
    api.get<LeagueDto[]>(`/sport-levels/${sportLevelId}/leagues`, { params: { includeInactive } }).then((r) => r.data),
  createLeague: (sportLevelId: string, req: CreateLeagueRequest) =>
    api.post<LeagueDto>(`/sport-levels/${sportLevelId}/leagues`, req).then((r) => r.data),
  updateLeague: (id: string, req: UpdateLeagueRequest) =>
    api.put<LeagueDto>(`/leagues/${id}`, req).then((r) => r.data),
  deleteLeague: (id: string) => api.delete(`/leagues/${id}`).then(() => undefined),

  // Conferences
  listConferences: (leagueId: string, includeInactive = false) =>
    api.get<ConferenceDto[]>(`/leagues/${leagueId}/conferences`, { params: { includeInactive } }).then((r) => r.data),
  createConference: (leagueId: string, req: CreateConferenceRequest) =>
    api.post<ConferenceDto>(`/leagues/${leagueId}/conferences`, req).then((r) => r.data),
  updateConference: (id: string, req: UpdateConferenceRequest) =>
    api.put<ConferenceDto>(`/conferences/${id}`, req).then((r) => r.data),
  deleteConference: (id: string) => api.delete(`/conferences/${id}`).then(() => undefined),

  // Teams (under a league, optional conference)
  listTeams: (leagueId: string, includeInactive = false) =>
    api.get<TeamDto[]>(`/leagues/${leagueId}/teams`, { params: { includeInactive } }).then((r) => r.data),
  createTeam: (leagueId: string, req: CreateTeamRequest) =>
    api.post<TeamDto>(`/leagues/${leagueId}/teams`, req).then((r) => r.data),
  updateTeam: (id: string, req: UpdateTeamRequest) =>
    api.put<TeamDto>(`/teams/${id}`, req).then((r) => r.data),
  deleteTeam: (id: string) => api.delete(`/teams/${id}`).then(() => undefined),
}

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
