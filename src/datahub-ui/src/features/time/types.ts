// Domain-facing time types. An `IDatasetTimeProfile` is what a viewer page
// declares about its dataset; the TimeBar shows the registered profiles in
// a dropdown and the active profile's bounds + granularity drive the slider.
//
// A `TimeWindow` is the live state: [from, to] in epoch-ms plus the active
// granularity and profile id. Components reading from TimeRangeContext only
// ever see a TimeWindow; the profile registry is a separate concern they
// don't have to think about.

import type { GranularityId, TimeMs } from './granularity'

export interface IDatasetTimeProfile {
  /** Stable id, used in URL (`?profile=sports.teams`). */
  id: string
  /** Human-readable label for the profile picker. */
  label: string
  /** Default granularity for new sessions on this profile. */
  granularity: GranularityId
  /** Inclusive lower bound the slider can reach. */
  minDate: TimeMs
  /** Inclusive upper bound the slider can reach. */
  maxDate: TimeMs
  /** Playback step in granularity buckets per second. */
  defaultStep: number
  /**
   * Optional explicit snap points (epoch-ms). When provided, the slider
   * snaps both handles to the nearest snap point on commit. When absent,
   * the slider snaps to granularity-aligned bucket boundaries.
   */
  snapPoints?: TimeMs[]
}

export interface TimeWindow {
  from: TimeMs
  to: TimeMs
  granularity: GranularityId
  /** Active profile id, or null if no profiles are registered. */
  profileId: string | null
}

export interface TimeRangeApi {
  window: TimeWindow
  /** Setters; values are clamped + granularity-snapped before commit. */
  setRange: (from: TimeMs, to: TimeMs) => void
  setFrom: (from: TimeMs) => void
  setTo: (to: TimeMs) => void
  setGranularity: (g: GranularityId) => void
  setProfile: (profileId: string) => void

  // Profile registry surface
  profiles: IDatasetTimeProfile[]
  activeProfile: IDatasetTimeProfile | null
  registerProfile: (p: IDatasetTimeProfile) => () => void

  // Playback
  isPlaying: boolean
  setIsPlaying: (v: boolean) => void
  /** Buckets per second; multiplied by the profile's `defaultStep` as base. */
  playbackSpeed: number
  setPlaybackSpeed: (v: number) => void
  playbackMode: 'expand' | 'slide'
  setPlaybackMode: (m: 'expand' | 'slide') => void
}
