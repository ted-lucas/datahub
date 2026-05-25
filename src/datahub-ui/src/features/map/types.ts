// Shared types for the map feature.
//
// Boundaries come from static GeoJSON / TopoJSON files under `/geo/*`; metrics
// come from `/api/geo/metrics`. They're joined client-side by FIPS (state and
// county) or by ISO-2 / UN M49 numeric code (country).

import type { FeatureCollection, Geometry } from 'geojson'

/** Level of admin boundary currently driving the map. */
export type GeoLevel = 'country' | 'state' | 'county'

/**
 * What's being counted in the choropleth. Must match the backend
 * `GeoMetricKind` enum (`Regions` | `Teams` | `Venues`).
 */
export type GeoMetricKind = 'regions' | 'teams' | 'venues'

/** Raw row returned by `GET /api/geo/metrics`. */
export interface GeoMetric {
  fips: string
  name: string
  count: number
}

/** Feature properties we standardize across levels for the choropleth join. */
export interface GeoFeatureProperties {
  /** Stable join key. ISO-2 (country, when available) or FIPS string. */
  joinKey: string
  name: string
  /** Filled in after the metric merge; undefined => "no data" styling. */
  metric?: number
}

export type GeoFeatureCollection = FeatureCollection<Geometry, GeoFeatureProperties>

/** Configuration for one drill-down level. */
export interface LevelConfig {
  level: GeoLevel
  /** Vector source id used in the MapLibre style. */
  sourceId: string
  /** Layer id (fill) used in the MapLibre style. */
  fillLayerId: string
  /** Layer id (outline) used in the MapLibre style. */
  lineLayerId: string
  /** Inclusive zoom range during which this level is visible. */
  minZoom: number
  maxZoom: number
}
