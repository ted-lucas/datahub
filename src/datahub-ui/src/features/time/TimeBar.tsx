// Footer bar shown on every authenticated page. Renders:
//   - the active profile picker (only visible if >1 profile is registered)
//   - the granularity picker (year / month / day / season)
//   - the dual-handle slider
//   - playback controls (play/pause, speed, mode)
// Collapses to a single status line on narrow viewports.

import {
  Box,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import PauseIcon from '@mui/icons-material/Pause'
import ReplayIcon from '@mui/icons-material/Replay'
import { useTimeRange } from './TimeRangeContext'
import { TimeSlider } from './TimeSlider'
import type { GranularityId } from './granularity'

const SPEEDS = [0.5, 1, 2, 5, 10]

export function TimeBar() {
  const api = useTimeRange()
  const profile = api.activeProfile
  const { window } = api

  // Without any registered profiles we still render the bar but in a
  // disabled state, so its presence is constant across pages.
  const disabled = !profile

  return (
    <Paper
      elevation={4}
      square
      sx={{
        position: 'fixed',
        left: 0,
        right: 0,
        bottom: 0,
        zIndex: (t) => t.zIndex.drawer + 2,
        py: 1,
        px: 2,
        borderTop: 1,
        borderColor: 'divider',
      }}
    >
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
        {/* Playback */}
        <Tooltip title={api.isPlaying ? 'Pause' : 'Play'}>
          <span>
            <IconButton
              size="small"
              onClick={() => api.setIsPlaying(!api.isPlaying)}
              disabled={disabled}
              color={api.isPlaying ? 'primary' : 'default'}
            >
              {api.isPlaying ? <PauseIcon /> : <PlayArrowIcon />}
            </IconButton>
          </span>
        </Tooltip>
        <Tooltip title="Reset to full range">
          <span>
            <IconButton
              size="small"
              onClick={() => profile && api.setRange(profile.minDate, profile.maxDate)}
              disabled={disabled}
            >
              <ReplayIcon />
            </IconButton>
          </span>
        </Tooltip>
        <TextField
          select
          size="small"
          label="Speed"
          value={api.playbackSpeed}
          onChange={(e) => api.setPlaybackSpeed(Number(e.target.value))}
          disabled={disabled}
          sx={{ width: 86 }}
        >
          {SPEEDS.map((s) => (
            <MenuItem key={s} value={s}>{s}×</MenuItem>
          ))}
        </TextField>
        <ToggleButtonGroup
          size="small"
          exclusive
          value={api.playbackMode}
          onChange={(_, v) => v && api.setPlaybackMode(v)}
          disabled={disabled}
        >
          <ToggleButton value="expand">Expand</ToggleButton>
          <ToggleButton value="slide">Slide</ToggleButton>
        </ToggleButtonGroup>

        {/* Slider */}
        <Box sx={{ flexGrow: 1, minWidth: 240 }}>
          {profile ? (
            <TimeSlider
              profile={profile}
              granularityId={window.granularity}
              from={window.from}
              to={window.to}
              onChange={api.setRange}
            />
          ) : (
            <Typography variant="caption" color="text.secondary" sx={{ ml: 2 }}>
              No time-aware dataset on this page.
            </Typography>
          )}
        </Box>

        {/* Granularity */}
        <TextField
          select
          size="small"
          label="Bucket"
          value={window.granularity}
          onChange={(e) => api.setGranularity(e.target.value as GranularityId)}
          disabled={disabled}
          sx={{ width: 110 }}
        >
          <MenuItem value="day">Day</MenuItem>
          <MenuItem value="month">Month</MenuItem>
          <MenuItem value="year">Year</MenuItem>
          <MenuItem value="season">Season</MenuItem>
        </TextField>

        {/* Profile picker (only when multiple registered) */}
        {api.profiles.length > 1 && (
          <TextField
            select
            size="small"
            label="Dataset"
            value={profile?.id ?? ''}
            onChange={(e) => api.setProfile(e.target.value)}
            sx={{ width: 160 }}
          >
            {api.profiles.map((p) => (
              <MenuItem key={p.id} value={p.id}>{p.label}</MenuItem>
            ))}
          </TextField>
        )}
      </Stack>
    </Paper>
  )
}
