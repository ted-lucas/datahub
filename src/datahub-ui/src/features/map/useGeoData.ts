// Loads the three static boundary files and normalizes them into GeoJSON
// FeatureCollections with a uniform `joinKey` property the rest of the map
// code can rely on. Results are module-cached so re-mounts don't refetch.

import { feature } from 'topojson-client'
import type { Topology, GeometryCollection } from 'topojson-specification'
import type { Feature, Geometry } from 'geojson'
import type { GeoFeatureCollection, GeoFeatureProperties } from './types'

const COUNTRIES_URL = '/geo/countries-110m.topo.json'
const STATES_URL = '/geo/us-states-10m.topo.json'
const COUNTIES_URL = '/geo/us-counties-10m.topo.json'

const cache = new Map<string, Promise<GeoFeatureCollection>>()

function memo(key: string, loader: () => Promise<GeoFeatureCollection>) {
  let p = cache.get(key)
  if (!p) {
    p = loader().catch((err) => {
      cache.delete(key) // allow retry on next call
      throw err
    })
    cache.set(key, p)
  }
  return p
}

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Failed to load ${url}: ${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

function normalizeFeature(
  f: Feature,
  joinKey: string,
  name: string,
): Feature<Geometry, GeoFeatureProperties> {
  return {
    type: 'Feature',
    geometry: f.geometry as Geometry,
    properties: { joinKey, name },
  }
}

export function loadCountries(): Promise<GeoFeatureCollection> {
  return memo('countries', async () => {
    const topo = await fetchJson<Topology>(COUNTRIES_URL)
    const obj = topo.objects.countries as GeometryCollection
    // feature() returns a GeoJSON FeatureCollection from a TopoJSON object.
    const fc = feature(topo, obj) as unknown as { features: Feature[] }
    return {
      type: 'FeatureCollection',
      features: fc.features.map((f) =>
        normalizeFeature(
          f,
          // World-atlas country `id` is a UN M49 numeric string; not ideal for
          // joining to ISO-2-keyed metrics, but adequate as a placeholder.
          String(f.id ?? ''),
          (f.properties as { name?: string })?.name ?? 'Unknown',
        ),
      ),
    }
  })
}

export function loadStates(): Promise<GeoFeatureCollection> {
  return memo('states', async () => {
    const topo = await fetchJson<Topology>(STATES_URL)
    const obj = topo.objects.states as GeometryCollection
    const fc = feature(topo, obj) as unknown as { features: Feature[] }
    return {
      type: 'FeatureCollection',
      features: fc.features.map((f) =>
        normalizeFeature(
          f,
          // us-atlas state id is the 2-digit state FIPS, e.g. "06".
          String(f.id ?? ''),
          (f.properties as { name?: string })?.name ?? 'Unknown',
        ),
      ),
    }
  })
}

export function loadCounties(): Promise<GeoFeatureCollection> {
  return memo('counties', async () => {
    // TopoJSON form of the us-atlas counties file. We switched away from the
    // GeoJSON variant because that file contains several non-simple polygons
    // (the well-known D3 us-atlas "dirty geometry" issue): D3 happily fills
    // them, but MapLibre's stricter earcut triangulator can't produce a valid
    // mesh and falls back to drawing the *wireframe* of its attempted
    // triangulation — which on screen looks like dense diagonal stripes
    // across the entire map. The TopoJSON form encodes shared arcs once with
    // unambiguous winding, and `feature()` reconstructs clean rings; bonus,
    // it's ~5x smaller on the wire (842 KB vs 3.6 MB).
    const topo = await fetchJson<Topology>(COUNTIES_URL)
    const obj = topo.objects.counties as GeometryCollection
    const fc = feature(topo, obj) as unknown as { features: Feature[] }
    return {
      type: 'FeatureCollection',
      features: fc.features.map((f) =>
        normalizeFeature(
          f,
          // us-atlas county id is the 5-digit county FIPS, e.g. "06037".
          String(f.id ?? ''),
          (f.properties as { name?: string })?.name ?? 'Unknown',
        ),
      ),
    }
  })
}
