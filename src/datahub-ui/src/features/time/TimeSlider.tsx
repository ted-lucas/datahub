// Dual-handle time slider. Operates in *bucket-index space* internally —
// from the profile's minDate to maxDate, divided into N buckets by the
// active granularity. The MUI Slider runs from 0..N; we translate to ms on
// commit. This keeps snapping trivially correct and tick math cheap.

import { Box, Slider, Stack, Typography } from '@mui/material'
import { useMemo } from 'react'
import { granularityOf, type TimeMs } from './granularity'
import type { IDatasetTimeProfile } from './types'

export interface TimeSliderProps {
  profile: IDatasetTimeProfile
  granularityId: import('./granularity').GranularityId
  from: TimeMs
  to: TimeMs
  onChange: (from: TimeMs, to: TimeMs) => void
  disabled?: boolean
}

export function TimeSlider(props: TimeSliderProps) {
  const { profile, granularityId, from, to, onChange, disabled } = props
  const strat = granularityOf(granularityId)

  // Bucket math. `min` is the profile's minDate floored to a bucket boundary;
  // `total` is the number of buckets in [min, maxDate]. Bucket 0 starts at
  // min; bucket total ends at maxDate.
  const { min, total } = useMemo(() => {
    const mn = strat.floor(profile.minDate)
    const mx = strat.ceil(profile.maxDate)
    return { min: mn, total: Math.max(1, strat.count(mn, mx)) }
  }, [profile.minDate, profile.maxDate, strat])

  const msToBucket = (t: TimeMs) => Math.max(0, Math.min(total, strat.count(min, strat.floor(t))))
  const bucketToMs = (b: number) => strat.step(min, b)

  const fromBucket = msToBucket(from)
  const toBucket = msToBucket(to)

  // Ticks: aim for ~5–10 across the slider.
  const ticks = useMemo(() => {
    const stride = strat.tickStride(total)
    const arr: { value: number; label: string }[] = []
    for (let b = 0; b <= total; b += stride) {
      arr.push({ value: b, label: strat.format(bucketToMs(b)) })
    }
    // Ensure the last tick is always shown.
    if (arr[arr.length - 1]?.value !== total) {
      arr.push({ value: total, label: strat.format(bucketToMs(total)) })
    }
    return arr
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [total, strat])

  const handleChange = (_: Event, value: number | number[]) => {
    if (!Array.isArray(value)) return
    const [a, b] = value
    if (a === fromBucket && b === toBucket) return
    onChange(bucketToMs(a), bucketToMs(Math.max(a + 1, b)))
  }

  return (
    <Box sx={{ px: 2, width: '100%' }}>
      <Stack direction="row" sx={{ alignItems: 'baseline', mb: 0.5 }} spacing={2}>
        <Typography variant="caption" color="text.secondary" sx={{ minWidth: 90 }}>
          {strat.format(from)}
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        <Typography variant="caption" color="text.secondary">
          {strat.format(strat.step(to, -1))}
        </Typography>
      </Stack>
      <Slider
        size="small"
        value={[fromBucket, toBucket]}
        onChange={handleChange}
        min={0}
        max={total}
        step={1}
        marks={ticks}
        disabled={disabled}
        valueLabelDisplay="auto"
        valueLabelFormat={(v) => strat.format(bucketToMs(v))}
        sx={{
          '& .MuiSlider-markLabel': { fontSize: 10, color: 'text.secondary' },
          '& .MuiSlider-mark': { height: 6 },
        }}
      />
    </Box>
  )
}
