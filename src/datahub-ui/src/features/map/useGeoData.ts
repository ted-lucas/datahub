// Loads the three static boundary files and normalizes them into GeoJSON
// FeatureCollections with a uniform `joinKey` property the rest of the map
// code can rely on. Results are module-cached so re-mounts don't refetch.

import { feature } from 'topojson-client'
import type { Topology, GeometryCollection } from 'topojson-specification'
import type { Feature, Geometry, Polygon, MultiPolygon, Position } from 'geojson'
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

// ── Antimeridian splitting ────────────────────────────────────────────────
// world-atlas countries-110m encodes Fiji, Russia, and Antarctica with rings
// that span ~360° of longitude (they include vertices on both sides of the
// ±180° seam). MapLibre's earcut triangulator sees a ring 360° wide and
// produces nonsense triangles spanning the whole canvas — visible as long
// horizontal/diagonal bands. The fix is to cut each offending ring at the
// antimeridian into two well-formed rings, one per hemisphere.
//
// Algorithm (practical, not strictly geodesic — sufficient for rendering):
//   1. Detect rings with a vertex-to-vertex longitude jump > 180°. Those
//      are the crossings: the source data went "the short way" round the
//      globe but the Cartesian renderer can only draw "the long way".
//   2. Unwrap the ring into a continuous longitude space [0, 360] by
//      shifting any lon < 0 by +360 whenever doing so makes consecutive
//      vertices closer together. After unwrapping, the ring lives entirely
//      east of 0 and spans somewhere across the lon=180 meridian.
//   3. Sutherland–Hodgman-clip the unwrapped ring twice: once against
//      lon ≤ 180 (the eastern half), once against lon ≥ 180 with the
//      result shifted back by −360 (the western half).
//   4. Drop empty clips. Both halves are well-formed rings that earcut can
//      triangulate cleanly.
// Antarctica works because world-atlas already includes the south-pole
// closure vertices in the source ring; unwrapping + clipping preserves
// them.

function ringSpan(ring: Position[]): number {
  let min = Infinity
  let max = -Infinity
  for (const [lon] of ring) {
    if (lon < min) min = lon
    if (lon > max) max = lon
  }
  return max - min
}

function unwrapRing(ring: Position[]): Position[] {
  // Make a fresh copy with all longitudes shifted into a single continuous
  // run. Start from the first vertex as-is; for each subsequent vertex,
  // pick the +360k offset that minimizes the jump from the previous vertex.
  const out: Position[] = []
  let prevLon: number | null = null
  for (const [lon, lat] of ring) {
    let nlon = lon
    if (prevLon !== null) {
      while (nlon - prevLon > 180) nlon -= 360
      while (nlon - prevLon < -180) nlon += 360
    }
    out.push([nlon, lat])
    prevLon = nlon
  }
  // Now shift the whole ring so it sits in [0, 360]-ish (i.e. lift any
  // negative longitudes by +360 in bulk if the min is negative).
  let minLon = Infinity
  for (const [lon] of out) if (lon < minLon) minLon = lon
  if (minLon < 0) {
    const shift = Math.ceil(-minLon / 360) * 360
    for (const p of out) p[0] += shift
  }
  return out
}

/** Sutherland–Hodgman clip against the vertical line lon = `x`, keeping the side `keep`. */
function clipRingAtMeridian(ring: Position[], x: number, keep: 'left' | 'right'): Position[] {
  if (ring.length === 0) return []
  const inside = (lon: number) => (keep === 'left' ? lon <= x : lon >= x)
  const intersect = (a: Position, b: Position): Position => {
    const t = (x - a[0]) / (b[0] - a[0])
    return [x, a[1] + t * (b[1] - a[1])]
  }
  const out: Position[] = []
  // Treat ring as closed: last vertex equals first in GeoJSON convention.
  const n = ring.length
  for (let i = 0; i < n; i++) {
    const cur = ring[i]
    const prev = ring[(i + n - 1) % n]
    const curIn = inside(cur[0])
    const prevIn = inside(prev[0])
    if (curIn) {
      if (!prevIn) out.push(intersect(prev, cur))
      out.push(cur)
    } else if (prevIn) {
      out.push(intersect(prev, cur))
    }
  }
  if (out.length === 0) return []
  // Re-close.
  const first = out[0]
  const last = out[out.length - 1]
  if (first[0] !== last[0] || first[1] !== last[1]) out.push([first[0], first[1]])
  return out
}

function splitRingAtAntimeridian(ring: Position[]): Position[][] {
  if (ringSpan(ring) <= 180) return [ring]

  // Strategy 1: assume a real antimeridian crossing (Fiji, Russia). Unwrap
  // to a continuous longitude run, then Sutherland–Hodgman-clip into two
  // halves on either side of lon=180.
  const unwrapped = unwrapRing(ring)
  const east = clipRingAtMeridian(unwrapped, 180, 'left')
  const westShifted = clipRingAtMeridian(unwrapped, 180, 'right')
  const west = westShifted.map<Position>(([lon, lat]) => [lon - 360, lat])
  if (east.length >= 4 && west.length >= 4) {
    return [east, west]
  }

  // Strategy 2: polar special-case (Antarctica). The ring already has
  // vertices at both ±180 and the "crossing" is actually the closure edge
  // running along the antimeridian at high latitude. There's no real
  // crossing to split — the ring just needs to be sealed via the pole.
  // Replace each seam-spanning edge with a detour `(cur_lon, polar) →
  // (next_lon, polar)`, where `polar` is whichever pole the ring is
  // closest to. The resulting horizontal edge at the pole is degenerate
  // geographically (the pole is a single point) but renders as a clean
  // trapezoidal flap, which is what every world map does for Antarctica.
  const lats = ring.map(([, lat]) => lat)
  const avgLat = lats.reduce((a, b) => a + b, 0) / lats.length
  const polarLat = avgLat < 0 ? -90 : 90
  const detoured: Position[] = []
  const n = ring.length
  for (let i = 0; i < n; i++) {
    const cur = ring[i]
    const next = ring[(i + 1) % n]
    detoured.push(cur)
    if (Math.abs(next[0] - cur[0]) > 180) {
      detoured.push([cur[0], polarLat])
      detoured.push([next[0], polarLat])
    }
  }
  if (
    detoured.length > 0 &&
    (detoured[0][0] !== detoured[detoured.length - 1][0] ||
      detoured[0][1] !== detoured[detoured.length - 1][1])
  ) {
    detoured.push([detoured[0][0], detoured[0][1]])
  }
  return [detoured]
}

function fixGeometry(g: Geometry | null): Geometry | null {
  if (!g) return g
  if (g.type === 'Polygon') {
    const rings = (g.coordinates as Position[][]).flatMap((r) => splitRingAtAntimeridian(r))
    // If splitting produced extra outer rings (one per hemisphere), promote to MultiPolygon.
    if (rings.length === g.coordinates.length) {
      return { type: 'Polygon', coordinates: rings } as Polygon
    }
    return { type: 'MultiPolygon', coordinates: rings.map((r) => [r]) } as MultiPolygon
  }
  if (g.type === 'MultiPolygon') {
    const polys: Position[][][] = []
    for (const poly of g.coordinates as Position[][][]) {
      const outerSplit = splitRingAtAntimeridian(poly[0])
      const holes = poly.slice(1)
      for (const o of outerSplit) polys.push([o, ...holes])
    }
    return { type: 'MultiPolygon', coordinates: polys } as MultiPolygon
  }
  return g
}

function normalizeFeature(
  f: Feature,
  joinKey: string,
  name: string,
): Feature<Geometry, GeoFeatureProperties> {
  return {
    type: 'Feature',
    geometry: fixGeometry(f.geometry as Geometry) as Geometry,
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
