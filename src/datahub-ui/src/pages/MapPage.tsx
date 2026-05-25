import { useMemo } from 'react'
import { Box, Typography } from '@mui/material'
import { MapView } from '../features/map/MapView'
import { useRegisterTimeProfile, useTimeRange } from '../features/time/TimeRangeContext'
import type { IDatasetTimeProfile } from '../features/time/types'

// The Map currently surfaces year-grained metrics (Teams + Venues both have
// FoundedYear / ClosedYear). 1850–today covers every realistic US pro team.
// Registering the profile here means the TimeBar's slider + granularity
// picker is anchored to this dataset whenever the Map is mounted.
const MAP_PROFILE: IDatasetTimeProfile = {
  id: 'sports.regions',
  label: 'Sports regions',
  granularity: 'year',
  minDate: Date.UTC(1850, 0, 1),
  maxDate: Date.UTC(new Date().getUTCFullYear() + 1, 0, 1),
  defaultStep: 1,
}

export default function MapPage() {
  useRegisterTimeProfile(MAP_PROFILE)
  const { window } = useTimeRange()
  // MapView is a pure consumer; pass the active window in so its metric
  // re-fetches whenever the slider moves.
  const time = useMemo(
    () => ({ from: window.from, to: window.to, granularity: window.granularity }),
    [window.from, window.to, window.granularity],
  )

  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Map
      </Typography>
      <MapView time={time} />
    </Box>
  )
}
