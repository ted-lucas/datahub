// MapView: zoom-driven drill-down choropleth.
//
// On mount we build a MapLibre instance with empty GeoJSON sources, then load
// boundary data + metrics for each level and push them into the already-mounted
// sources. Boundary geometry loads once and is module-cached; metrics re-fetch
// whenever the active `metric` (regions / teams / venues) changes, without
// re-downloading the polygons.
//
// Drill-down is purely zoom-based: layer minzoom/maxzoom in `mapStyle.ts`
// controls which level is visible. Clicks fly the camera in/out — the layer
// swap happens automatically once the new zoom is reached.

import { useEffect, useMemo, useRef, useState } from 'react'
import { Box, Chip, Paper, Stack, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material'
import maplibregl, { Map as MlMap } from 'maplibre-gl'
import type { MapGeoJSONFeature, MapMouseEvent } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import { buildStyle } from './mapStyle'
import { LEVELS } from './layers'
import { loadCountries, loadCounties, loadStates } from './useGeoData'
import { fetchMetrics, mergeMetrics } from './useGeoMetrics'
import type { GeoFeatureCollection, GeoFeatureProperties, GeoLevel, GeoMetricKind } from './types'

interface HoverState {
  level: GeoLevel
  name: string
  metric?: number
}

const METRIC_OPTIONS: Array<{ value: GeoMetricKind; label: string }> = [
  { value: 'regions', label: 'Regions' },
  { value: 'teams', label: 'Teams' },
  { value: 'venues', label: 'Venues' },
]

export function MapView() {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const mapRef = useRef<MlMap | null>(null)
  const hoveredRef = useRef<{ source: string; id: string | number } | null>(null)
  // Cache of loaded boundary FeatureCollections per level. Populated by the
  // boundary-load effect, then reused by the metric-hydration effect every
  // time the active metric changes.
  const boundariesRef = useRef<Partial<Record<GeoLevel, GeoFeatureCollection>>>({})
  const [styleReady, setStyleReady] = useState(false)
  const [hover, setHover] = useState<HoverState | null>(null)
  const [zoom, setZoom] = useState(2)
  const [metric, setMetric] = useState<GeoMetricKind>('regions')

  // For Phase 2 we only ship US data. Pass it as `parent` so the backend can
  // pre-filter Team/Venue rows by country at state level. (For country-level
  // metrics the backend ignores parent.)
  const stateParentByLevel = useMemo<Record<GeoLevel, string | undefined>>(
    () => ({ country: undefined, state: 'US', county: undefined }),
    [],
  )

  // ── Mount / unmount the MapLibre instance ──────────────────────────────
  useEffect(() => {
    if (!containerRef.current) return
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: buildStyle(),
      center: [-98, 39], // Continental US-ish; world is also visible at zoom 2.
      zoom: 2,
      minZoom: 1,
      maxZoom: 10,
      attributionControl: { compact: true },
    })
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'top-right')
    map.on('zoom', () => setZoom(map.getZoom()))
    map.on('load', () => setStyleReady(true))
    mapRef.current = map

    return () => {
      map.remove()
      mapRef.current = null
      setStyleReady(false)
      boundariesRef.current = {}
    }
  }, [])

  // ── Load boundary GeoJSON once, then push empty (no-metric) data into the
  //    sources so the basemap paints immediately. The metric-merge effect
  //    below will overlay choropleth shading.
  useEffect(() => {
    const map = mapRef.current
    if (!map || !styleReady) return
    let cancelled = false

    const loaders: Array<[GeoLevel, () => Promise<GeoFeatureCollection>]> = [
      ['country', loadCountries],
      ['state', loadStates],
      ['county', loadCounties],
    ]

    loaders.forEach(async ([level, load]) => {
      try {
        const fc = await load()
        if (cancelled) return
        boundariesRef.current[level] = fc
        const src = map.getSource(LEVELS[level].sourceId) as maplibregl.GeoJSONSource | undefined
        if (src) src.setData(fc as never)
      } catch (err) {
        console.warn(`[MapView] failed to load ${level} boundaries:`, err)
      }
    })

    return () => {
      cancelled = true
    }
  }, [styleReady])

  // ── Hydrate (or re-hydrate) metrics whenever the active metric changes ──
  useEffect(() => {
    const map = mapRef.current
    if (!map || !styleReady) return
    let cancelled = false

    const hydrate = async (level: GeoLevel) => {
      try {
        const fc = boundariesRef.current[level]
        // Wait for boundaries (the other effect may not have completed yet).
        // We retry by re-running this effect once boundaries are in via the
        // styleReady flip, but on first paint they may genuinely not be ready.
        if (!fc) return
        const rows = await fetchMetrics(level, stateParentByLevel[level], metric)
        if (cancelled) return
        const merged = mergeMetrics(fc, rows)
        const src = map.getSource(LEVELS[level].sourceId) as maplibregl.GeoJSONSource | undefined
        if (src) src.setData(merged as never)
      } catch (err) {
        console.warn(`[MapView] failed to fetch ${level} metrics:`, err)
      }
    }

    void Promise.all([hydrate('country'), hydrate('state'), hydrate('county')])

    return () => {
      cancelled = true
    }
    // `styleReady` so we re-run after boundaries first arrive.
  }, [metric, styleReady, stateParentByLevel])

  // ── Hover + click interactions per level ───────────────────────────────
  useEffect(() => {
    const map = mapRef.current
    if (!map) return

    const clearHover = () => {
      if (hoveredRef.current) {
        map.setFeatureState(hoveredRef.current, { hover: false })
        hoveredRef.current = null
      }
      setHover(null)
      map.getCanvas().style.cursor = ''
    }

    const onMove =
      (level: GeoLevel) =>
      (e: MapMouseEvent & { features?: MapGeoJSONFeature[] }) => {
        const f = e.features?.[0]
        if (!f) return
        const props = f.properties as GeoFeatureProperties
        const source = LEVELS[level].sourceId
        const id = (f.id ?? props.joinKey) as string | number
        if (
          hoveredRef.current &&
          (hoveredRef.current.source !== source || hoveredRef.current.id !== id)
        ) {
          map.setFeatureState(hoveredRef.current, { hover: false })
        }
        hoveredRef.current = { source, id }
        map.setFeatureState(hoveredRef.current, { hover: true })
        map.getCanvas().style.cursor = 'pointer'
        setHover({ level, name: props.name, metric: props.metric })
      }

    // Click drills down by flying into the level's typical view zoom.
    const drillTargetZoom: Record<GeoLevel, number> = {
      country: 4.5, // -> reveals states
      state: 6.5, // -> reveals counties
      county: 8.5, // -> stay at county level
    }
    const onClick =
      (level: GeoLevel) =>
      (e: MapMouseEvent & { features?: MapGeoJSONFeature[] }) => {
        const f = e.features?.[0]
        if (!f) return
        map.flyTo({ center: e.lngLat, zoom: drillTargetZoom[level], speed: 1.2 })
      }

    const handlers: Array<() => void> = []
    ;(Object.keys(LEVELS) as GeoLevel[]).forEach((level) => {
      const layer = LEVELS[level].fillLayerId
      const move = onMove(level)
      const click = onClick(level)
      map.on('mousemove', layer, move)
      map.on('mouseleave', layer, clearHover)
      map.on('click', layer, click)
      handlers.push(() => {
        map.off('mousemove', layer, move)
        map.off('mouseleave', layer, clearHover)
        map.off('click', layer, click)
      })
    })

    return () => handlers.forEach((h) => h())
  }, [])

  return (
    <Box sx={{ position: 'relative', width: '100%', height: 'calc(100vh - 112px)' }}>
      <div ref={containerRef} style={{ width: '100%', height: '100%' }} />

      {/* Top-left: status chips */}
      <Stack
        direction="row"
        spacing={1}
        sx={{ position: 'absolute', top: 12, left: 12, pointerEvents: 'none' }}
      >
        <Chip
          size="small"
          color="primary"
          label={`zoom ${zoom.toFixed(1)} · ${activeLevel(zoom)}`}
        />
        {hover && (
          <Chip
            size="small"
            label={`${hover.name}${hover.metric != null ? ` · ${hover.metric}` : ''}`}
          />
        )}
      </Stack>

      {/* Bottom-left: metric picker */}
      <Paper
        elevation={3}
        sx={{
          position: 'absolute',
          bottom: 24,
          left: 12,
          px: 1.5,
          py: 1,
          display: 'flex',
          flexDirection: 'column',
          gap: 0.5,
        }}
      >
        <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600 }}>
          Metric
        </Typography>
        <ToggleButtonGroup
          value={metric}
          exclusive
          size="small"
          onChange={(_, next: GeoMetricKind | null) => {
            if (next) setMetric(next)
          }}
        >
          {METRIC_OPTIONS.map((opt) => (
            <ToggleButton key={opt.value} value={opt.value}>
              {opt.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      </Paper>
    </Box>
  )
}

function activeLevel(zoom: number): GeoLevel {
  if (zoom >= LEVELS.county.minZoom) return 'county'
  if (zoom >= LEVELS.state.minZoom) return 'state'
  return 'country'
}
