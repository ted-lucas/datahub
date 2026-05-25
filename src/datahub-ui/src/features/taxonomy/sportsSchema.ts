// Sports taxonomy schema. Mirrors the API shape from sportsApi exactly;
// the only "intelligence" here is splitting League's children into two
// groupings (Conferences + Teams) and giving Conference both Conferences
// (recursive) and Teams as children — using parentConferenceId /
// conferenceId on the DTOs to filter the right buckets.

import {
  sportsApi,
  type ConferenceDto,
  type CreateConferenceRequest,
  type CreateLeagueRequest,
  type CreateSportLevelRequest,
  type CreateSportRequest,
  type CreateTeamRequest,
  type LeagueDto,
  type SportDto,
  type SportLevelDto,
  type TeamDto,
  type UpdateConferenceRequest,
  type UpdateLeagueRequest,
  type UpdateSportLevelRequest,
  type UpdateSportRequest,
  type UpdateTeamRequest,
} from '../../api/endpoints'
import type { ChildGrouping, LevelDescriptor, TaxonomyNode, TaxonomySchema } from './types'

// ── node factories ─────────────────────────────────────────────────────────
const sportNode = (d: SportDto, parent: TaxonomyNode | null = null): TaxonomyNode<SportDto> => ({
  id: d.id, levelId: 'sport', label: d.name, raw: d, parent,
})
const levelNode = (d: SportLevelDto, parent: TaxonomyNode): TaxonomyNode<SportLevelDto> => ({
  id: d.id, levelId: 'sportLevel', label: d.name, raw: d, parent,
})
const leagueNode = (d: LeagueDto, parent: TaxonomyNode): TaxonomyNode<LeagueDto> => ({
  id: d.id, levelId: 'league', label: d.abbreviation ? `${d.name} (${d.abbreviation})` : d.name, raw: d, parent,
})
const conferenceNode = (d: ConferenceDto, parent: TaxonomyNode): TaxonomyNode<ConferenceDto> => ({
  id: d.id, levelId: 'conference', label: d.name, raw: d, parent,
})
const teamNode = (d: TeamDto, parent: TaxonomyNode): TaxonomyNode<TeamDto> => ({
  id: d.id, levelId: 'team', label: d.name, raw: d, parent,
})

// ── ancestor lookups for recursive conferences ─────────────────────────────
function ancestorOfLevel(node: TaxonomyNode | null, levelId: string): TaxonomyNode | null {
  let cur = node
  while (cur && cur.levelId !== levelId) cur = cur.parent
  return cur
}

// ── child groupings ────────────────────────────────────────────────────────
const sportLevelsChildren: ChildGrouping = {
  childLevelId: 'sportLevel',
  label: 'Levels',
  load: async (parent) => {
    const list = await sportsApi.listLevels(parent.id, true)
    return list.map((d) => levelNode(d, parent))
  },
}

const levelLeaguesChildren: ChildGrouping = {
  childLevelId: 'league',
  label: 'Leagues',
  load: async (parent) => {
    const list = await sportsApi.listLeagues(parent.id, true)
    return list.map((d) => leagueNode(d, parent))
  },
}

const leagueConferencesChildren: ChildGrouping = {
  childLevelId: 'conference',
  label: 'Conferences',
  load: async (parent) => {
    // Only top-level conferences (parentConferenceId === null) hang off the league directly.
    const list = await sportsApi.listConferences(parent.id, true)
    return list.filter((c) => !c.parentConferenceId).map((d) => conferenceNode(d, parent))
  },
}

const leagueTeamsChildren: ChildGrouping = {
  childLevelId: 'team',
  label: 'Teams (unassigned)',
  load: async (parent) => {
    // Only teams without a conference hang off the league directly.
    const list = await sportsApi.listTeams(parent.id, true)
    return list.filter((t) => !t.conferenceId).map((d) => teamNode(d, parent))
  },
}

const conferenceSubConferencesChildren: ChildGrouping = {
  childLevelId: 'conference',
  label: 'Sub-conferences',
  load: async (parent) => {
    // Need to query under the league, then filter by parentConferenceId === parent.id.
    const league = ancestorOfLevel(parent, 'league')
    if (!league) return []
    const list = await sportsApi.listConferences(league.id, true)
    return list.filter((c) => c.parentConferenceId === parent.id).map((d) => conferenceNode(d, parent))
  },
}

const conferenceTeamsChildren: ChildGrouping = {
  childLevelId: 'team',
  label: 'Teams',
  load: async (parent) => {
    const league = ancestorOfLevel(parent, 'league')
    if (!league) return []
    const list = await sportsApi.listTeams(league.id, true)
    return list.filter((t) => t.conferenceId === parent.id).map((d) => teamNode(d, parent))
  },
}

// ── level descriptors ──────────────────────────────────────────────────────
const sportLevel: LevelDescriptor<SportDto> = {
  singular: 'Sport',
  plural: 'Sports',
  fields: [
    { name: 'name', label: 'Name', type: 'text', required: true },
    { name: 'slug', label: 'Slug', type: 'text', required: true, helperText: 'URL-safe id' },
    { name: 'iconRef', label: 'Icon ref', type: 'text' },
    { name: 'sortOrder', label: 'Sort order', type: 'number' },
  ],
  children: [sportLevelsChildren],
  create: async (_parent, v) => {
    const req: CreateSportRequest = {
      name: v.name, slug: v.slug, iconRef: v.iconRef || null, sortOrder: Number(v.sortOrder) || 0,
    }
    const d = await sportsApi.createSport(req)
    return sportNode(d)
  },
  update: async (node, v) => {
    const req: UpdateSportRequest = {
      name: v.name, slug: v.slug, iconRef: v.iconRef || null,
      sortOrder: Number(v.sortOrder) || 0, isActive: !!v.isActive,
    }
    const d = await sportsApi.updateSport(node.id, req)
    return sportNode(d, node.parent)
  },
  remove: async (node) => { await sportsApi.deleteSport(node.id) },
}

const sportLevelLevel: LevelDescriptor<SportLevelDto> = {
  singular: 'Level',
  plural: 'Levels',
  fields: [
    { name: 'name', label: 'Name', type: 'text', required: true },
    { name: 'sortOrder', label: 'Sort order', type: 'number' },
  ],
  children: [levelLeaguesChildren],
  create: async (parent, v) => {
    if (!parent) throw new Error('Level requires a Sport parent')
    const req: CreateSportLevelRequest = { name: v.name, sortOrder: Number(v.sortOrder) || 0 }
    const d = await sportsApi.createLevel(parent.id, req)
    return levelNode(d, parent)
  },
  update: async (node, v) => {
    const req: UpdateSportLevelRequest = {
      name: v.name, sortOrder: Number(v.sortOrder) || 0, isActive: !!v.isActive,
    }
    const d = await sportsApi.updateLevel(node.id, req)
    return levelNode(d, node.parent!)
  },
  remove: async (node) => { await sportsApi.deleteLevel(node.id) },
}

const leagueLevel: LevelDescriptor<LeagueDto> = {
  singular: 'League',
  plural: 'Leagues',
  fields: [
    { name: 'name', label: 'Name', type: 'text', required: true },
    { name: 'abbreviation', label: 'Abbreviation', type: 'text' },
    { name: 'country', label: 'Country', type: 'text', helperText: 'ISO-2 (US, CA, …)' },
    { name: 'foundedYear', label: 'Founded year', type: 'number' },
  ],
  children: [leagueConferencesChildren, leagueTeamsChildren],
  create: async (parent, v) => {
    if (!parent) throw new Error('League requires a Level parent')
    const req: CreateLeagueRequest = {
      name: v.name, abbreviation: v.abbreviation || null,
      country: v.country || null, foundedYear: v.foundedYear ? Number(v.foundedYear) : null,
    }
    const d = await sportsApi.createLeague(parent.id, req)
    return leagueNode(d, parent)
  },
  update: async (node, v) => {
    const req: UpdateLeagueRequest = {
      name: v.name, abbreviation: v.abbreviation || null,
      country: v.country || null, foundedYear: v.foundedYear ? Number(v.foundedYear) : null,
      isActive: !!v.isActive,
    }
    const d = await sportsApi.updateLeague(node.id, req)
    return leagueNode(d, node.parent!)
  },
  remove: async (node) => { await sportsApi.deleteLeague(node.id) },
}

const conferenceLevel: LevelDescriptor<ConferenceDto> = {
  singular: 'Conference',
  plural: 'Conferences',
  fields: [
    { name: 'name', label: 'Name', type: 'text', required: true },
  ],
  children: [conferenceSubConferencesChildren, conferenceTeamsChildren],
  create: async (parent, v) => {
    if (!parent) throw new Error('Conference requires a League or Conference parent')
    const league = ancestorOfLevel(parent, 'league')
    if (!league) throw new Error('No League ancestor')
    const parentConferenceId = parent.levelId === 'conference' ? parent.id : null
    const req: CreateConferenceRequest = { name: v.name, parentConferenceId }
    const d = await sportsApi.createConference(league.id, req)
    return conferenceNode(d, parent)
  },
  update: async (node, v) => {
    const req: UpdateConferenceRequest = {
      name: v.name,
      parentConferenceId: (node.raw as ConferenceDto).parentConferenceId,
      isActive: !!v.isActive,
    }
    const d = await sportsApi.updateConference(node.id, req)
    return conferenceNode(d, node.parent!)
  },
  remove: async (node) => { await sportsApi.deleteConference(node.id) },
}

const teamLevel: LevelDescriptor<TeamDto> = {
  singular: 'Team',
  plural: 'Teams',
  fields: [
    { name: 'name', label: 'Name', type: 'text', required: true, colSpan: 2 },
    { name: 'city', label: 'City', type: 'text' },
    { name: 'state', label: 'State', type: 'text', helperText: 'Postal (MO, CA, …)' },
    { name: 'country', label: 'Country', type: 'text', helperText: 'ISO-2 (US, CA, …)' },
    { name: 'foundedYear', label: 'Founded year', type: 'number' },
    { name: 'primaryColor', label: 'Primary color', type: 'text', helperText: '#RRGGBB' },
    { name: 'secondaryColor', label: 'Secondary color', type: 'text', helperText: '#RRGGBB' },
    { name: 'logoRef', label: 'Logo ref', type: 'text', colSpan: 2 },
  ],
  children: [],
  create: async (parent, v) => {
    if (!parent) throw new Error('Team requires a League or Conference parent')
    const league = ancestorOfLevel(parent, 'league')
    if (!league) throw new Error('No League ancestor')
    const conferenceId = parent.levelId === 'conference' ? parent.id : null
    const req: CreateTeamRequest = {
      name: v.name, conferenceId, venueId: null,
      city: v.city || null, state: v.state || null, country: v.country || null,
      foundedYear: v.foundedYear ? Number(v.foundedYear) : null,
      primaryColor: v.primaryColor || null, secondaryColor: v.secondaryColor || null,
      logoRef: v.logoRef || null,
    }
    const d = await sportsApi.createTeam(league.id, req)
    return teamNode(d, parent)
  },
  update: async (node, v) => {
    const raw = node.raw as TeamDto
    const req: UpdateTeamRequest = {
      name: v.name,
      conferenceId: raw.conferenceId,
      venueId: raw.venueId,
      city: v.city || null, state: v.state || null, country: v.country || null,
      foundedYear: v.foundedYear ? Number(v.foundedYear) : null,
      primaryColor: v.primaryColor || null, secondaryColor: v.secondaryColor || null,
      logoRef: v.logoRef || null, isActive: !!v.isActive,
    }
    const d = await sportsApi.updateTeam(node.id, req)
    return teamNode(d, node.parent!)
  },
  remove: async (node) => { await sportsApi.deleteTeam(node.id) },
}

export const sportsSchema: TaxonomySchema = {
  title: 'Sports Taxonomy',
  rootLevelId: 'sport',
  loadRoots: async () => {
    const list = await sportsApi.listSports(true)
    return list.map((d) => sportNode(d))
  },
  levels: {
    sport: sportLevel,
    sportLevel: sportLevelLevel,
    league: leagueLevel,
    conference: conferenceLevel,
    team: teamLevel,
  },
}
