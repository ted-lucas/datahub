// Time-axis primitives. Everything in the time subsystem (slider, context,
// URL sync, playback, dataset profiles) speaks two types: a `TimeMs` (epoch
// milliseconds, always UTC) and a `GranularityId`. Per-granularity behavior
// lives in `GranularityStrategy` objects; consumers never branch on the
// granularity id, they call methods on the strategy.

export type TimeMs = number

export type GranularityId = 'day' | 'month' | 'year' | 'season'

/**
 * Per-granularity behavior. All implementations operate on UTC, so the same
 * input ms always produces the same boundary regardless of the user's local
 * timezone. (Display formatting can still localize separately if needed.)
 */
export interface GranularityStrategy {
  id: GranularityId
  /** Singular label, e.g. "Year". Used by the granularity picker. */
  label: string
  /** Snap `t` down to the start of the bucket it lives in. */
  floor(t: TimeMs): TimeMs
  /** Snap `t` up to the start of the *next* bucket (exclusive end). */
  ceil(t: TimeMs): TimeMs
  /** Advance by `n` buckets (negative allowed). Result is bucket-aligned iff input was. */
  step(t: TimeMs, n: number): TimeMs
  /** Number of complete buckets between `a` and `b` (a ≤ b, both floored). */
  count(a: TimeMs, b: TimeMs): number
  /** Human-readable label for a single bucket starting at `t`. */
  format(t: TimeMs): string
  /**
   * Recommended major-tick stride in *buckets* given the total bucket-span
   * being rendered, so the slider doesn't draw 200 yearly ticks at 100 px wide.
   * Implementations should return a stride such that ~5–12 ticks render.
   */
  tickStride(totalBuckets: number): number
}

// ── helpers ────────────────────────────────────────────────────────────────
const MS_PER_DAY = 86_400_000

function utc(y: number, m = 0, d = 1): TimeMs {
  return Date.UTC(y, m, d)
}

function ymd(t: TimeMs): { y: number; m: number; d: number } {
  const dt = new Date(t)
  return { y: dt.getUTCFullYear(), m: dt.getUTCMonth(), d: dt.getUTCDate() }
}

/** Pick a "nice" stride from a candidate ladder so we get ~5–12 ticks. */
function niceStride(totalBuckets: number, ladder: number[]): number {
  for (const s of ladder) {
    const count = Math.ceil(totalBuckets / s)
    if (count <= 12) return s
  }
  return ladder[ladder.length - 1]
}

// ── day ────────────────────────────────────────────────────────────────────
export const dayGranularity: GranularityStrategy = {
  id: 'day',
  label: 'Day',
  floor(t) {
    const { y, m, d } = ymd(t)
    return utc(y, m, d)
  },
  ceil(t) {
    const f = this.floor(t)
    return f === t ? t : f + MS_PER_DAY
  },
  step(t, n) {
    return t + n * MS_PER_DAY
  },
  count(a, b) {
    return Math.round((b - a) / MS_PER_DAY)
  },
  format(t) {
    return new Date(t).toISOString().slice(0, 10)
  },
  tickStride(totalBuckets) {
    // 1d, 7d, 30d, 90d, 180d, 365d.
    return niceStride(totalBuckets, [1, 7, 30, 90, 180, 365, 365 * 2, 365 * 5])
  },
}

// ── month ──────────────────────────────────────────────────────────────────
export const monthGranularity: GranularityStrategy = {
  id: 'month',
  label: 'Month',
  floor(t) {
    const { y, m } = ymd(t)
    return utc(y, m, 1)
  },
  ceil(t) {
    const f = this.floor(t)
    return f === t ? t : this.step(f, 1)
  },
  step(t, n) {
    const { y, m, d } = ymd(t)
    return utc(y, m + n, d)
  },
  count(a, b) {
    const A = ymd(a)
    const B = ymd(b)
    return (B.y - A.y) * 12 + (B.m - A.m)
  },
  format(t) {
    const { y, m } = ymd(t)
    return `${y}-${String(m + 1).padStart(2, '0')}`
  },
  tickStride(totalBuckets) {
    return niceStride(totalBuckets, [1, 3, 6, 12, 24, 60, 120])
  },
}

// ── year ───────────────────────────────────────────────────────────────────
export const yearGranularity: GranularityStrategy = {
  id: 'year',
  label: 'Year',
  floor(t) {
    return utc(ymd(t).y)
  },
  ceil(t) {
    const f = this.floor(t)
    return f === t ? t : utc(ymd(t).y + 1)
  },
  step(t, n) {
    const { y, m, d } = ymd(t)
    return utc(y + n, m, d)
  },
  count(a, b) {
    return ymd(b).y - ymd(a).y
  },
  format(t) {
    return String(ymd(t).y)
  },
  tickStride(totalBuckets) {
    return niceStride(totalBuckets, [1, 2, 5, 10, 25, 50, 100])
  },
}

// ── season (US sports: Sep YYYY → Aug YYYY+1) ──────────────────────────────
// A season is identified by its starting year. The bucket spans Sep 1 of
// year Y through Aug 31 of year Y+1; its bucket start is Sep 1, Y.

const SEASON_START_MONTH = 8 // September (0-indexed)

function seasonStartYear(t: TimeMs): number {
  const { y, m } = ymd(t)
  return m >= SEASON_START_MONTH ? y : y - 1
}

export const seasonGranularity: GranularityStrategy = {
  id: 'season',
  label: 'Season',
  floor(t) {
    return utc(seasonStartYear(t), SEASON_START_MONTH, 1)
  },
  ceil(t) {
    const f = this.floor(t)
    return f === t ? t : utc(seasonStartYear(t) + 1, SEASON_START_MONTH, 1)
  },
  step(t, n) {
    const sy = seasonStartYear(t)
    return utc(sy + n, SEASON_START_MONTH, 1)
  },
  count(a, b) {
    return seasonStartYear(b) - seasonStartYear(a)
  },
  format(t) {
    const sy = seasonStartYear(t)
    const next = (sy + 1) % 100
    return `${sy}-${String(next).padStart(2, '0')}`
  },
  tickStride(totalBuckets) {
    return niceStride(totalBuckets, [1, 2, 5, 10, 25, 50, 100])
  },
}

// ── registry ───────────────────────────────────────────────────────────────
export const granularities: Record<GranularityId, GranularityStrategy> = {
  day: dayGranularity,
  month: monthGranularity,
  year: yearGranularity,
  season: seasonGranularity,
}

export function granularityOf(id: GranularityId): GranularityStrategy {
  return granularities[id]
}
