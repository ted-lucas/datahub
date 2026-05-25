import type { LevelConfig } from './types'

// LOD: which boundary layer is visible at which zoom range.
//
// MapLibre treats layer visibility as `minzoom <= z < maxzoom`, so the bands
// below are *disjoint by construction*. Overlapping bands were responsible
// for the cross-hatching / moir\u00e9 artifact at intermediate zooms — when two
// fill layers with `fill-opacity < 1` paint the same pixel, alpha-blending
// produces darker patches everywhere their polygon edges fail to align
// (which happens constantly: us-atlas state polygons are coastline-clipped
// slightly differently from the union of their county polygons, so the two
// layers leave thin slivers of mismatched fill along every coast and state
// border).
//
// Hand-off zooms (4 country\u2192state, 6 state\u2192county) are chosen so the
// next layer is detailed enough to be useful at the moment the previous one
// disappears. Tune here if the swap feels too early/late.
export const LEVELS: Record<'country' | 'state' | 'county', LevelConfig> = {
  country: {
    level: 'country',
    sourceId: 'geo-countries',
    fillLayerId: 'geo-countries-fill',
    lineLayerId: 'geo-countries-line',
    minZoom: 0,
    maxZoom: 4,
  },
  state: {
    level: 'state',
    sourceId: 'geo-states',
    fillLayerId: 'geo-states-fill',
    lineLayerId: 'geo-states-line',
    minZoom: 4,
    maxZoom: 6,
  },
  county: {
    level: 'county',
    sourceId: 'geo-counties',
    fillLayerId: 'geo-counties-fill',
    lineLayerId: 'geo-counties-line',
    minZoom: 6,
    maxZoom: 22,
  },
}
