<script lang="ts">
  import { geoNaturalEarth1, geoPath } from 'd3-geo'
  import { zoom as d3Zoom, zoomIdentity } from 'd3-zoom'
  import { select } from 'd3-selection'
  import { feature } from 'topojson-client'
  import type { Topology, GeometryCollection } from 'topojson-specification'
  import type { FeatureCollection } from 'geojson'
  // @ts-ignore
  import worldData from 'world-atlas/countries-110m.json'
  import topoMap from '../../assets/topoMap.json'
  import type { DiscoveryCountrySummary } from '../../stores/game.svelte'

  interface Props {
    countries: DiscoveryCountrySummary[]
  }

  let { countries }: Props = $props()

  let containerEl: HTMLElement | undefined = $state()
  let svgEl: SVGSVGElement | undefined = $state()
  let width = $state(600)
  let height = $state(260)
  let transform = $state({ x: 0, y: 0, k: 1 })
  let hoveredName = $state<string | null>(null)
  let pinnedName = $state<string | null>(null)

  const world = worldData as unknown as Topology
  const countriesGeo = feature(
    world,
    world.objects.countries as GeometryCollection,
  ) as FeatureCollection

  const projection = geoNaturalEarth1()
  const pathGen = geoPath().projection(projection)

  const reverseTopoMap = Object.fromEntries(
    Object.entries(topoMap).map(([displayName, atlasName]) => [atlasName, displayName]),
  ) as Record<string, string>

  const discoveryByAtlasName = $derived.by(() => {
    const byName = new Map<string, DiscoveryCountrySummary>()
    for (const country of countries) {
      const mapped = topoMap[country.name as keyof typeof topoMap]
      if (mapped) byName.set(mapped, country)
    }
    return byName
  })

  const paths = $derived.by(() => {
    if (width <= 0 || height <= 0) return []
    projection.fitSize([width, height], countriesGeo)
    return countriesGeo.features.map((feat) => {
      const atlasName = (feat.properties as Record<string, unknown>)?.name as string | undefined
      const country = atlasName ? discoveryByAtlasName.get(atlasName) ?? null : null
      return {
        d: pathGen(feat) ?? '',
        atlasName: atlasName ?? null,
        country,
      }
    })
  })

  const activeCountry = $derived.by(() => {
    const activeName = hoveredName || pinnedName
    if (!activeName) return null
    return countries.find((country) => country.name === activeName) ?? null
  })

  const tooltipTitle = $derived(activeCountry?.name ?? 'Hover or tap a cōntry')

  const tooltipBody = $derived.by(() => {
    if (!activeCountry) return ''
    if (!activeCountry.discovered || activeCountry.best_attempts == null) {
      return 'Not discovered yet.'
    }

    const parts = [`Found in ${activeCountry.best_attempts} ${activeCountry.best_attempts === 1 ? 'try' : 'tries'}`]
    if (activeCountry.last_solved_at) {
      parts.push(new Date(activeCountry.last_solved_at).toLocaleDateString())
    }
    return parts.join(' • ')
  })

  function getThemeColor(name: string, fallback: string) {
    if (typeof document === 'undefined') return fallback
    const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
    return value || fallback
  }

  function getFillColor(country: DiscoveryCountrySummary | null) {
    if (!country?.discovered || country.best_attempts == null) return getThemeColor('--panel-3', '#2b3031')
    if (country.best_attempts === 1) return getThemeColor('--info', '#458588')
    if (country.best_attempts <= 3) return getThemeColor('--ok', '#98971a')
    if (country.best_attempts <= 5) return getThemeColor('--warn', '#d79921')
    if (country.best_attempts <= 9) return '#fe8019'
    return getThemeColor('--bad', '#cc241d')
  }

  function handleCountryEnter(displayName: string | null) {
    if (!displayName) return
    hoveredName = displayName
  }

  function handleCountryLeave() {
    hoveredName = null
  }

  function handleCountryTap(displayName: string | null) {
    if (!displayName) return
    pinnedName = pinnedName === displayName ? null : displayName
  }

  function clearPinned() {
    pinnedName = null
  }

  $effect(() => {
    if (!svgEl) return

    const zoomBehavior = d3Zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 8])
      .on('zoom', (event) => {
        transform = {
          x: event.transform.x,
          y: event.transform.y,
          k: event.transform.k,
        }
      })

    const sel = select(svgEl)
    sel.call(zoomBehavior)

    return () => {
      sel.on('.zoom', null)
    }
  })

  $effect(() => {
    if (!containerEl) return
    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const w = Math.round(entry.contentRect.width)
        if (w > 0) {
          width = w
          height = Math.max(180, Math.round(w * 0.55))
        }
      }
    })
    ro.observe(containerEl)
    return () => ro.disconnect()
  })
</script>

<!-- svelte-ignore a11y_click_events_have_key_events -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div class="map-shell" bind:this={containerEl} onclick={clearPinned}>
  <div class="map-tooltip">
    <div class="tooltip-title">{tooltipTitle}</div>
    {#if tooltipBody}
      <div class="tooltip-body">{tooltipBody}</div>
    {/if}
  </div>

  <svg bind:this={svgEl} class="world-map" viewBox={`0 0 ${width} ${height}`}>
    <rect x="0" y="0" width={width} height={height} fill="var(--panel)" />

    <g transform={`translate(${transform.x},${transform.y}) scale(${transform.k})`}>
      {#each paths as path}
        <path
          d={path.d}
          class="country"
          fill={getFillColor(path.country)}
          onmouseenter={() => handleCountryEnter(path.atlasName ? reverseTopoMap[path.atlasName] ?? path.atlasName : null)}
          onmouseleave={handleCountryLeave}
          onclick={(e) => {
            e.stopPropagation()
            handleCountryTap(path.atlasName ? reverseTopoMap[path.atlasName] ?? path.atlasName : null)
          }}
        />
      {/each}
    </g>
  </svg>
</div>

<style>
  .map-shell {
    position: relative;
    width: 100%;
    flex: 0 0 auto;
    aspect-ratio: 1.7;
    min-height: 180px;
    max-height: 280px;
    border-radius: 12px;
    overflow: hidden;
    background: var(--panel);
  }

  .world-map {
    width: 100%;
    height: 100%;
    display: block;
    cursor: grab;
    touch-action: none;
  }

  .world-map:active {
    cursor: grabbing;
  }

  .country {
    stroke: var(--border-strong);
    stroke-width: 0.4;
    vector-effect: non-scaling-stroke;
    transition: fill 0.2s ease, stroke 0.2s ease;
  }

  @media (hover: hover) {
    .country:hover {
      stroke: var(--chip-bg);
      stroke-width: 0.8;
    }
  }

  .map-tooltip {
    position: absolute;
    top: 12px;
    right: 12px;
    max-width: min(150px, calc(100% - 24px));
    padding: 7px 9px;
    border-radius: 8px;
    background: var(--tooltip-bg);
    color: var(--tooltip-text);
    box-shadow: var(--shadow-lift);
    z-index: 2;
  }

  .tooltip-title {
    font-size: 11px;
    font-weight: 700;
    line-height: 1.2;
  }

  .tooltip-body {
    margin-top: 2px;
    font-size: 10px;
    line-height: 1.3;
  }

  @media (max-height: 740px) {
    .map-shell {
      aspect-ratio: 1.85;
      min-height: 150px;
      max-height: 220px;
    }
  }
</style>
