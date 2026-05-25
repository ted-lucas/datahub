// TimeRangeProvider: single source of truth for the whole app's time window,
// active dataset profile, granularity, and playback state. URL is the
// authoritative serialization (`?from&to&g&profile`); on mount we hydrate
// from URL, and on every change we replaceState back so reloads + share
// links Just Work.

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { granularityOf, type GranularityId, type TimeMs } from './granularity'
import type { IDatasetTimeProfile, TimeRangeApi, TimeWindow } from './types'

const VALID_GRAN: ReadonlySet<GranularityId> = new Set(['day', 'month', 'year', 'season'])

const TimeRangeContext = createContext<TimeRangeApi | null>(null)

export function useTimeRange(): TimeRangeApi {
  const ctx = useContext(TimeRangeContext)
  if (!ctx) throw new Error('useTimeRange must be used inside <TimeRangeProvider>')
  return ctx
}

/** Read `?from&to&g&profile` from current URL (returns nulls for missing). */
function readUrl(): {
  from: TimeMs | null
  to: TimeMs | null
  g: GranularityId | null
  profileId: string | null
} {
  if (typeof window === 'undefined') return { from: null, to: null, g: null, profileId: null }
  const p = new URLSearchParams(window.location.search)
  const fromRaw = p.get('from')
  const toRaw = p.get('to')
  const gRaw = p.get('g') as GranularityId | null
  return {
    from: fromRaw ? Number(fromRaw) : null,
    to: toRaw ? Number(toRaw) : null,
    g: gRaw && VALID_GRAN.has(gRaw) ? gRaw : null,
    profileId: p.get('profile'),
  }
}

/** Write `?from&to&g&profile` back to the URL without polluting history. */
function writeUrl(w: TimeWindow) {
  if (typeof window === 'undefined') return
  const p = new URLSearchParams(window.location.search)
  p.set('from', String(w.from))
  p.set('to', String(w.to))
  p.set('g', w.granularity)
  if (w.profileId) p.set('profile', w.profileId)
  else p.delete('profile')
  const next = `${window.location.pathname}?${p.toString()}${window.location.hash}`
  window.history.replaceState(window.history.state, '', next)
}

/** Clamp [from, to] to the profile's bounds and snap to granularity. */
function normalizeWindow(
  from: TimeMs,
  to: TimeMs,
  g: GranularityId,
  bounds: { min: TimeMs; max: TimeMs },
): { from: TimeMs; to: TimeMs } {
  const strat = granularityOf(g)
  let f = strat.floor(Math.max(bounds.min, Math.min(bounds.max, from)))
  let t = strat.ceil(Math.max(bounds.min, Math.min(bounds.max, to)))
  if (t <= f) t = strat.step(f, 1)
  if (t > bounds.max) t = strat.ceil(bounds.max)
  if (f < bounds.min) f = strat.floor(bounds.min)
  return { from: f, to: t }
}

interface ProviderProps {
  children: ReactNode
  /**
   * Bootstrap profile used when no profiles are registered yet (so the
   * footer can still render meaningful bounds). Kept intentionally wide
   * (1900–next-year) — viewer pages override it by registering their own.
   */
  fallbackProfile?: IDatasetTimeProfile
}

const DEFAULT_FALLBACK: IDatasetTimeProfile = {
  id: '__default__',
  label: 'All time',
  granularity: 'year',
  minDate: Date.UTC(1900, 0, 1),
  maxDate: Date.UTC(new Date().getUTCFullYear() + 1, 0, 1),
  defaultStep: 1,
}

export function TimeRangeProvider({ children, fallbackProfile = DEFAULT_FALLBACK }: ProviderProps) {
  // Profile registry: viewer pages call registerProfile() in a useEffect.
  // Stored in a ref so re-renders don't tear down the registry, and surfaced
  // through a counter that we bump to trigger re-renders when it changes.
  const profilesRef = useRef<Map<string, IDatasetTimeProfile>>(new Map())
  const [profilesRev, setProfilesRev] = useState(0)

  const registerProfile = useCallback((p: IDatasetTimeProfile) => {
    profilesRef.current.set(p.id, p)
    setProfilesRev((r) => r + 1)
    return () => {
      profilesRef.current.delete(p.id)
      setProfilesRev((r) => r + 1)
    }
  }, [])

  // Active profile id. Hydrated from URL on first render, then either
  // (a) stays at whatever was in URL until that profile registers, or
  // (b) falls back to the first registered profile, or
  // (c) falls back to the fallbackProfile id (null).
  const initialUrl = useMemo(readUrl, [])
  const [profileId, setProfileIdRaw] = useState<string | null>(initialUrl.profileId)

  // Derived: which profile is actually active right now?
  const activeProfile = useMemo<IDatasetTimeProfile | null>(() => {
    const fromUrl = profileId ? profilesRef.current.get(profileId) : null
    if (fromUrl) return fromUrl
    const first = profilesRef.current.values().next().value as IDatasetTimeProfile | undefined
    return first ?? null
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profileId, profilesRev])

  const effectiveProfile = activeProfile ?? fallbackProfile

  // Window state. Initialized from URL if present, else from the effective
  // profile's full range. When the effective profile changes (e.g. because
  // a viewer page registered its profile), we re-clamp the window into the
  // new bounds.
  const [window, setWindowRaw] = useState<TimeWindow>(() => {
    const g: GranularityId = initialUrl.g ?? effectiveProfile.granularity
    const { from, to } = normalizeWindow(
      initialUrl.from ?? effectiveProfile.minDate,
      initialUrl.to ?? effectiveProfile.maxDate,
      g,
      { min: effectiveProfile.minDate, max: effectiveProfile.maxDate },
    )
    return { from, to, granularity: g, profileId: initialUrl.profileId ?? effectiveProfile.id }
  })

  // When the effective profile changes (registration, picker), reclamp +
  // adopt its granularity unless URL already pinned one. We deliberately
  // *don't* reset from/to to the profile's full range — the existing window
  // is preserved and just clamped, which feels much more natural when
  // navigating between viewers that share a time concern.
  const prevProfileIdRef = useRef<string | null>(effectiveProfile.id)
  useEffect(() => {
    const prev = prevProfileIdRef.current
    if (prev === effectiveProfile.id) return
    prevProfileIdRef.current = effectiveProfile.id
    setWindowRaw((cur) => {
      const g = cur.granularity ?? effectiveProfile.granularity
      const { from, to } = normalizeWindow(cur.from, cur.to, g, {
        min: effectiveProfile.minDate,
        max: effectiveProfile.maxDate,
      })
      return { from, to, granularity: g, profileId: effectiveProfile.id }
    })
  }, [effectiveProfile])

  // Persist window to URL on every change.
  useEffect(() => {
    writeUrl(window)
  }, [window])

  // Setters
  const setRange = useCallback(
    (from: TimeMs, to: TimeMs) => {
      setWindowRaw((cur) => {
        const { from: f, to: t } = normalizeWindow(from, to, cur.granularity, {
          min: effectiveProfile.minDate,
          max: effectiveProfile.maxDate,
        })
        return { ...cur, from: f, to: t }
      })
    },
    [effectiveProfile],
  )

  const setFrom = useCallback((from: TimeMs) => setRange(from, window.to), [setRange, window.to])
  const setTo = useCallback((to: TimeMs) => setRange(window.from, to), [setRange, window.from])

  const setGranularity = useCallback(
    (g: GranularityId) => {
      setWindowRaw((cur) => {
        const { from, to } = normalizeWindow(cur.from, cur.to, g, {
          min: effectiveProfile.minDate,
          max: effectiveProfile.maxDate,
        })
        return { ...cur, granularity: g, from, to }
      })
    },
    [effectiveProfile],
  )

  const setProfile = useCallback((id: string) => setProfileIdRaw(id), [])

  // Playback state
  const [isPlaying, setIsPlaying] = useState(false)
  const [playbackSpeed, setPlaybackSpeed] = useState(1) // multiplier on profile.defaultStep
  const [playbackMode, setPlaybackMode] = useState<'expand' | 'slide'>('expand')

  // Playback engine. Uses requestAnimationFrame; advances `to` (and `from`
  // in slide mode) by `defaultStep * speed` buckets per second. Stops when
  // `to` reaches the profile's max bound.
  useEffect(() => {
    if (!isPlaying) return
    let raf = 0
    let last = performance.now()
    const tick = (now: number) => {
      const dtSec = (now - last) / 1000
      last = now
      setWindowRaw((cur) => {
        const strat = granularityOf(cur.granularity)
        const bucketsPerSec = effectiveProfile.defaultStep * playbackSpeed
        const buckets = bucketsPerSec * dtSec
        // Use fractional bucket advance via ms math: convert one bucket to
        // ms by stepping from cur.to once, then scale.
        const oneBucketMs = strat.step(cur.to, 1) - cur.to
        const advanceMs = oneBucketMs * buckets
        let nextTo = cur.to + advanceMs
        let nextFrom = cur.from
        if (playbackMode === 'slide') {
          nextFrom = cur.from + advanceMs
        }
        if (nextTo >= effectiveProfile.maxDate) {
          nextTo = effectiveProfile.maxDate
          setIsPlaying(false)
        }
        const { from, to } = normalizeWindow(nextFrom, nextTo, cur.granularity, {
          min: effectiveProfile.minDate,
          max: effectiveProfile.maxDate,
        })
        return { ...cur, from, to }
      })
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [isPlaying, playbackSpeed, playbackMode, effectiveProfile])

  const profiles = useMemo(
    () => Array.from(profilesRef.current.values()),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [profilesRev],
  )

  const value: TimeRangeApi = {
    window,
    setRange,
    setFrom,
    setTo,
    setGranularity,
    setProfile,
    profiles,
    activeProfile,
    registerProfile,
    isPlaying,
    setIsPlaying,
    playbackSpeed,
    setPlaybackSpeed,
    playbackMode,
    setPlaybackMode,
  }

  return <TimeRangeContext.Provider value={value}>{children}</TimeRangeContext.Provider>
}

/**
 * Convenience hook for viewer pages: register a profile on mount, unregister
 * on unmount. The profile object should be stable (memoize it!) or the
 * register/unregister cycle will thrash.
 */
export function useRegisterTimeProfile(profile: IDatasetTimeProfile) {
  const { registerProfile } = useTimeRange()
  useEffect(() => registerProfile(profile), [registerProfile, profile])
}
