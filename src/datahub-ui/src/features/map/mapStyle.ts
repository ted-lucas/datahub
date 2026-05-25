// Minimal, self-contained MapLibre style spec.
//
// No external basemap (no Mapbox / MapTiler / OSM tile server) — just a flat
// background and the three boundary layers. This keeps the dev experience
// zero-config: no API keys, no token, no rate limits. A real basemap can be
// dropped in later by adding a `raster` or `vector` source above `sources`.

import type { StyleSpecification } from 'maplibre-gl'
import { LEVELS } from './layers'

/**
 * Build a fresh style. All sources start empty; `MapView` swaps in data via
 * `map.getSource(id).setData(...)` once GeoJSON loads.
 *
 * The color scale is data-driven via `interpolate` on the `metric` feature
 * property, so updating metrics doesn't require re-mounting the map.
 */
export function buildStyle(): StyleSpecification {
  return {
    version: 8,
    // OS-sourced glyphs would normally point at a glyph endpoint; we don't
    // render any text labels in the minimal style, so we omit it. Add a
    // glyphs URL here when introducing symbol layers.
    sources: {
      [LEVELS.country.sourceId]: {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
        promoteId: 'joinKey',
      },
      [LEVELS.state.sourceId]: {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
        promoteId: 'joinKey',
      },
      [LEVELS.county.sourceId]: {
        type: 'geojson',
        data: { type: 'FeatureCollection', features: [] },
        promoteId: 'joinKey',
      },
    },
    layers: [
      {
        id: 'background',
        type: 'background',
        paint: { 'background-color': '#0e1116' },
      },
      // Countries
      {
        id: LEVELS.country.fillLayerId,
        type: 'fill',
        source: LEVELS.country.sourceId,
        minzoom: LEVELS.country.minZoom,
        maxzoom: LEVELS.country.maxZoom,
        paint: {
          'fill-color': metricFillExpression(),
          // Bands in `layers.ts` are disjoint, so nothing renders underneath
          // the active level — full opacity gives the cleanest choropleth read
          // and avoids the alpha-blend cross-hatching that semi-transparent
          // overlapping layers used to produce. Hover still reads via the
          // brighter outline below rather than a fill-opacity bump.
          'fill-opacity': 1,
        },
      },
      {
        id: LEVELS.country.lineLayerId,
        type: 'line',
        source: LEVELS.country.sourceId,
        minzoom: LEVELS.country.minZoom,
        maxzoom: LEVELS.country.maxZoom,
        paint: {
          'line-color': [
            'case',
            ['boolean', ['feature-state', 'hover'], false],
            '#9ad0ff',
            '#2a3340',
          ],
          'line-width': [
            'case',
            ['boolean', ['feature-state', 'hover'], false],
            1.5,
            0.5,
          ],
        },
      },
      // States
      {
        id: LEVELS.state.fillLayerId,
        type: 'fill',
        source: LEVELS.state.sourceId,
        minzoom: LEVELS.state.minZoom,
        maxzoom: LEVELS.state.maxZoom,
        paint: {
          'fill-color': metricFillExpression(),
          'fill-opacity': 1,
        },
      },
      {
        id: LEVELS.state.lineLayerId,
        type: 'line',
        source: LEVELS.state.sourceId,
        minzoom: LEVELS.state.minZoom,
        maxzoom: LEVELS.state.maxZoom,
        paint: {
          'line-color': [
            'case',
            ['boolean', ['feature-state', 'hover'], false],
            '#9ad0ff',
            '#2a3340',
          ],
          'line-width': [
            'case',
            ['boolean', ['feature-state', 'hover'], false],
            1.5,
            0.7,
          ],
        },
      },
      // Counties
      {
        id: LEVELS.county.fillLayerId,
        type: 'fill',
        source: LEVELS.county.sourceId,
        minzoom: LEVELS.county.minZoom,
        maxzoom: LEVELS.county.maxZoom,
        paint: {
          'fill-color': metricFillExpression(),
          'fill-opacity': 1,
        },
      },
      {
        id: LEVELS.county.lineLayerId,
        type: 'line',
        source: LEVELS.county.sourceId,
        minzoom: LEVELS.county.minZoom,
        maxzoom: LEVELS.county.maxZoom,
        paint: {
          'line-color': [
            'case',
            ['boolean', ['feature-state', 'hover'], false],
            '#9ad0ff',
            '#2a3340',
          ],
          // Scale outline width with zoom so it stays visible (not sub-pixel)
          // when counties are large on screen, but doesn't dominate when
          // zoomed out into the band's lower edge. Without this it renders
          // as a 0.3 px hairline that aliases into a moir\u00e9-like pattern.
          //
          // Structure note: MapLibre forbids ["zoom"] inside a "case" branch
          // ("zoom" must be the *top-level* input to interpolate/step). So
          // we invert: interpolate over zoom at the outside, and let each
          // stop pick its width via a case on hover.
          'line-width': [
            'interpolate',
            ['linear'],
            ['zoom'],
            6, ['case', ['boolean', ['feature-state', 'hover'], false], 1.5, 0.4],
            8, ['case', ['boolean', ['feature-state', 'hover'], false], 1.8, 0.8],
            10, ['case', ['boolean', ['feature-state', 'hover'], false], 2.2, 1.2],
          ],
        },
      },
    ],
  }
}

/**
 * Data-driven fill: metric-less features get the "no data" gray;
 * everything else uses a small sequential blue ramp. Domain stops are
 * arbitrary defaults — adjust once real metrics drive the scale.
 */
function metricFillExpression() {
  return [
    'case',
    ['==', ['typeof', ['get', 'metric']], 'number'],
    [
      'interpolate',
      ['linear'],
      ['to-number', ['get', 'metric']],
      0, '#1f3147',
      1, '#2a4a6b',
      10, '#3e7cb1',
      100, '#5fa8d3',
      1000, '#a8d5ef',
    ],
    '#1a1f27', // no-data
  ] as unknown as import('maplibre-gl').DataDrivenPropertyValueSpecification<string>
}
