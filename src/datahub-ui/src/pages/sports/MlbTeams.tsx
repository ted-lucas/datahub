import { useEffect, useMemo, useState } from 'react'
import { Alert, Box, Chip, CircularProgress, Paper, Stack, Typography } from '@mui/material'
import { DataGrid, type GridColDef } from '@mui/x-data-grid'
import {
  sportsApi,
  type ConferenceDto,
  type LeagueDto,
  type SportDto,
  type SportLevelDto,
  type TeamDto,
} from '../../api/endpoints'
import { useRegisterTimeProfile, useTimeRange } from '../../features/time/TimeRangeContext'
import type { IDatasetTimeProfile } from '../../features/time/types'

// MLB-specific Grid viewer. First instance of §12.8 step 5 (Grid viewer pattern).
// Hardcoded to Baseball → Professional → MLB; we resolve the league id at mount
// by walking the existing nested endpoints (no need for a slug-lookup endpoint
// while there's exactly one league of interest).
//
// Time semantics: active-during-window per §12.1.3. The server's /api/teams
// endpoint already applies the filter when from/to are supplied.

const MLB_PROFILE: IDatasetTimeProfile = {
  id: 'sports.teams.mlb',
  label: 'MLB teams',
  granularity: 'year',
  minDate: Date.UTC(1850, 0, 1),
  maxDate: Date.UTC(new Date().getUTCFullYear() + 1, 0, 1),
  defaultStep: 1,
}

interface Resolved {
  league: LeagueDto
  conferences: Map<string, ConferenceDto>
}

async function resolveMlb(): Promise<Resolved | null> {
  const sports = await sportsApi.listSports()
  const baseball = sports.find((s: SportDto) => s.slug === 'baseball')
  if (!baseball) return null
  const levels = await sportsApi.listLevels(baseball.id)
  const pro = levels.find((l: SportLevelDto) => l.name === 'Professional')
  if (!pro) return null
  const leagues = await sportsApi.listLeagues(pro.id)
  const mlb = leagues.find((l: LeagueDto) => l.abbreviation === 'MLB' || l.name === 'Major League Baseball')
  if (!mlb) return null
  const conferences = await sportsApi.listConferences(mlb.id)
  return {
    league: mlb,
    conferences: new Map(conferences.map((c) => [c.id, c])),
  }
}

/** Walk Conference parent chain to find the top-level (e.g. AL/NL) for a division. */
function topLevelName(confId: string | null, lookup: Map<string, ConferenceDto>): string {
  if (!confId) return '—'
  let cur = lookup.get(confId)
  while (cur?.parentConferenceId) {
    const next = lookup.get(cur.parentConferenceId)
    if (!next) break
    cur = next
  }
  return cur?.name ?? '—'
}

export default function MlbTeams() {
  useRegisterTimeProfile(MLB_PROFILE)
  const { window } = useTimeRange()

  const [resolved, setResolved] = useState<Resolved | null>(null)
  const [teams, setTeams] = useState<TeamDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // 1. Resolve league + conferences once.
  useEffect(() => {
    resolveMlb()
      .then((r) => {
        if (!r) throw new Error('Baseball/Professional/MLB not found in taxonomy.')
        setResolved(r)
      })
      .catch((e) => {
        setError(e?.message ?? 'Failed to resolve MLB league')
        setLoading(false)
      })
  }, [])

  // 2. Re-fetch teams whenever resolved or the time window changes.
  useEffect(() => {
    if (!resolved) return
    setLoading(true)
    sportsApi
      .queryTeams({
        leagueId: resolved.league.id,
        time: { from: window.from, to: window.to, g: window.granularity },
      })
      .then(setTeams)
      .catch((e) => setError(e?.message ?? 'Failed to load teams'))
      .finally(() => setLoading(false))
  }, [resolved, window.from, window.to, window.granularity])

  const columns: GridColDef<TeamDto>[] = useMemo(
    () => [
      { field: 'name', headerName: 'Team', flex: 1, minWidth: 140 },
      { field: 'city', headerName: 'City', flex: 1, minWidth: 120 },
      { field: 'state', headerName: 'State', width: 80 },
      { field: 'country', headerName: 'Country', width: 90 },
      { field: 'foundedYear', headerName: 'Founded', width: 100, type: 'number' },
      { field: 'closedYear', headerName: 'Closed', width: 90, type: 'number',
        valueFormatter: (v) => (v == null ? '—' : String(v)) },
      {
        field: 'league',
        headerName: 'League',
        width: 110,
        valueGetter: (_v, row) => topLevelName(row.conferenceId, resolved?.conferences ?? new Map()),
      },
      {
        field: 'division',
        headerName: 'Division',
        width: 130,
        valueGetter: (_v, row) =>
          row.conferenceId ? resolved?.conferences.get(row.conferenceId)?.name ?? '—' : '—',
      },
      {
        field: 'isActive',
        headerName: 'Active',
        width: 90,
        renderCell: (p) => (
          <Chip
            size="small"
            label={p.value ? 'Active' : 'Inactive'}
            color={p.value ? 'success' : 'default'}
            variant={p.value ? 'filled' : 'outlined'}
          />
        ),
      },
    ],
    [resolved],
  )

  return (
    <Box>
      <Stack direction="row" spacing={2} alignItems="baseline" sx={{ mb: 2 }}>
        <Typography variant="h4">MLB Teams</Typography>
        <Typography variant="body2" color="text.secondary">
          {loading ? 'Loading…' : `${teams.length} team${teams.length === 1 ? '' : 's'} active in selected window`}
        </Typography>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper sx={{ height: 'calc(100vh - 260px)', minHeight: 400 }}>
        {loading && !teams.length ? (
          <Box sx={{ p: 4, display: 'flex', justifyContent: 'center' }}>
            <CircularProgress />
          </Box>
        ) : (
          <DataGrid
            rows={teams}
            columns={columns}
            getRowId={(r) => r.id}
            density="compact"
            initialState={{
              sorting: { sortModel: [{ field: 'name', sort: 'asc' }] },
              pagination: { paginationModel: { pageSize: 25 } },
            }}
            pageSizeOptions={[25, 50, 100]}
            disableRowSelectionOnClick
          />
        )}
      </Paper>
    </Box>
  )
}
