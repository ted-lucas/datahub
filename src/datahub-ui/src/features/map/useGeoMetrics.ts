// Fetches choropleth metrics from `/api/geo/metrics` and merges them into the
// `metric` property of every feature in a FeatureCollection (by `joinKey`).
//
// Pure-ish: returns a new FeatureCollection rather than mutating.

import { api } from '../../api/client'
import type { GeoFeatureCollection, GeoLevel, GeoMetric, GeoMetricKind } from './types'

export async function fetchMetrics(
  level: GeoLevel,
  parentFips?: string,
  kind: GeoMetricKind = 'regions',
): Promise<GeoMetric[]> {
  const params: Record<string, string> = { level, metric: kind }
  if (parentFips) params.parent = parentFips
  const res = await api.get<GeoMetric[]>('/geo/metrics', { params })
  return res.data
}

export function mergeMetrics(
  features: GeoFeatureCollection,
  metrics: GeoMetric[],
): GeoFeatureCollection {
  const byFips = new Map(metrics.map((m) => [m.fips, m.count]))
  return {
    type: 'FeatureCollection',
    features: features.features.map((f) => ({
      ...f,
      properties: {
        ...f.properties,
        metric: byFips.get(f.properties.joinKey),
      },
    })),
  }
}
