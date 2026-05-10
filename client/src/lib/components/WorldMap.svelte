<script lang="ts">
  import { geoNaturalEarth1, geoPath } from 'd3-geo'
  import { zoom as d3Zoom, zoomIdentity } from 'd3-zoom'
  import { select } from 'd3-selection'
  import 'd3-transition'
  import { feature } from 'topojson-client'
  // @ts-ignore — world-atlas doesn't have TS types
  import worldData from 'world-atlas/countries-110m.json'
  import type { Topology, GeometryCollection } from 'topojson-specification'
  import type { FeatureCollection } from 'geojson'
  import topoMap from '../assets/topoMap.json'

  interface Props {
    flyTo?: { lat: number; lon: number; zoom: number } | null
    highlightCountry?: string | null
    markerPosition?: { lat: number; lon: number } | null
    guessedCountries?: string[]
    errorCountry?: string | null
    correctCountry?: string | null
    gaveUpCountry?: string | null
  }

  let {
    flyTo = null,
    highlightCountry = null,
    markerPosition = null,
    guessedCountries = [],
    errorCountry = null,
    correctCountry = null,
    gaveUpCountry = null,
  }: Props = $props()

  let svgEl: SVGSVGElement | undefined = $state()
  let containerEl: HTMLElement | undefined = $state()
  let width = $state(600)
  let height = $state(400)
  let transform = $state({ x: 0, y: 0, k: 1 })

  const world = worldData as unknown as Topology
  const countriesGeo = feature(
    world,
    world.objects.countries as GeometryCollection
  ) as FeatureCollection

  const projection = geoNaturalEarth1()
  const pathGen = geoPath().projection(projection)

  // Derive paths from width/height — projection.fitSize is called inside the derivation
  // so it's always in sync. No separate effect needed.
  const paths = $derived.by(() => {
    if (width <= 0 || height <= 0) return []
    projection.fitSize([width, height], countriesGeo)
    return countriesGeo.features.map((feat) => ({
      d: pathGen(feat) ?? '',
      name: (feat.properties as Record<string, unknown>)?.name as string | undefined,
    }))
  })

  // Derive marker position from width/height too (projection is fitted inside paths derivation
  // which runs first due to template order, but let's be safe and also fit here)
  const markerXY = $derived.by(() => {
    if (!markerPosition || width <= 0 || height <= 0) return null
    projection.fitSize([width, height], countriesGeo)
    const p = projection([markerPosition.lon, markerPosition.lat])
    return p ? { x: p[0], y: p[1] } : null
  })

  // Pre-calculate mapped sets/values for reactivity
  const mappedHighlight = $derived(highlightCountry ? topoMap[highlightCountry as keyof typeof topoMap] : null)
  const mappedError = $derived(errorCountry ? topoMap[errorCountry as keyof typeof topoMap] : null)
  const mappedCorrect = $derived(correctCountry ? topoMap[correctCountry as keyof typeof topoMap] : null)
  const mappedGaveUp = $derived(gaveUpCountry ? topoMap[gaveUpCountry as keyof typeof topoMap] : null)
  const mappedGuessed = $derived(new Set(guessedCountries.map(c => topoMap[c as keyof typeof topoMap]).filter(Boolean)))

  // d3-zoom setup
  $effect(() => {
    if (!svgEl) return

    const zoomBehavior = d3Zoom<SVGSVGElement, unknown>()
      .scaleExtent([1, 12])
      .on('zoom', (event) => {
        transform = {
          x: event.transform.x,
          y: event.transform.y,
          k: event.transform.k,
        }
      })

    const sel = select(svgEl)
    sel.call(zoomBehavior)
    sel.on('dblclick.zoom', null)

    // Fly-to — nested effect reacts to flyTo changes
    $effect(() => {
      if (flyTo && svgEl && width > 0 && height > 0) {
        projection.fitSize([width, height], countriesGeo)
        const projected = projection([flyTo.lon, flyTo.lat])
        if (projected) {
          const targetK = Math.max(2, Math.min(flyTo.zoom, 10))
          const tx = width / 2 - projected[0] * targetK
          const ty = height / 2 - projected[1] * targetK

          sel
            .transition()
            .duration(800)
            .call(
              zoomBehavior.transform,
              zoomIdentity.translate(tx, ty).scale(targetK),
            )
        }
      }
    })

    return () => {
      sel.on('.zoom', null)
    }
  })

  // Resize observer — watches the container div, not the SVG
  $effect(() => {
    if (!containerEl) return
    const ro = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const w = Math.round(entry.contentRect.width)
        const h = Math.round(entry.contentRect.height)
        if (w > 0 && h > 0) {
          width = w
          height = h
        }
      }
    })
    ro.observe(containerEl)
    return () => ro.disconnect()
  })
</script>

<div class="map-container" bind:this={containerEl}>
  <svg
    bind:this={svgEl}
    class="world-map"
    viewBox="0 0 {width} {height}"
  >
    <!-- Ocean background -->
    <rect x="0" y="0" width={width} height={height} fill="var(--panel)" />

    <g transform="translate({transform.x},{transform.y}) scale({transform.k})">
      {#each paths as p}
        <path
          d={p.d}
          class="country"
          class:highlighted={mappedHighlight != null && p.name === mappedHighlight}
          class:guessed={p.name != null && mappedGuessed.has(p.name)}
          class:error={mappedError === p.name}
          class:correct={mappedCorrect === p.name}
          class:gave-up={mappedGaveUp === p.name}
        />
      {/each}

      {#if markerXY && !errorCountry && !gaveUpCountry}
        <g class="marker" transform="translate({markerXY.x},{markerXY.y})">
          <circle r={10 / transform.k} fill="var(--accent)" opacity="0.25" />
          <circle r={5 / transform.k} fill="var(--accent)" />
          <text
            y={-12 / transform.k}
            text-anchor="middle"
            font-size={14 / transform.k}
            fill="var(--accent)"
            font-weight="700"
          >?</text>
        </g>
      {/if}
    </g>
  </svg>
</div>

<style>
  .map-container {
    width: 100%;
    height: 100%;
    overflow: hidden;
    border-radius: 10px;
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
    fill: var(--panel-3);
    stroke: color-mix(in oklab, var(--border) 74%, var(--text));
    stroke-width: 0.4;
    vector-effect: non-scaling-stroke;
    transition: fill 0.25s ease;
  }

  .country.guessed {
    fill: color-mix(in oklab, var(--panel) 82%, var(--panel-3));
    stroke: var(--border);
  }

  .country.highlighted {
    fill: color-mix(in oklab, var(--accent) 55%, var(--panel-3));
    stroke: var(--accent);
    stroke-width: 1;
  }

  .country.error {
    fill: color-mix(in oklab, var(--bad) 35%, var(--panel));
    stroke: color-mix(in oklab, var(--bad) 75%, var(--panel-3));
    transition: none;
  }

  .country.correct {
    fill: color-mix(in oklab, var(--ok) 40%, var(--panel));
    stroke: color-mix(in oklab, var(--ok) 80%, var(--panel-3));
  }

  .country.gave-up {
    fill: color-mix(in oklab, var(--info) 40%, var(--panel));
    stroke: color-mix(in oklab, var(--info) 80%, var(--panel-3));
  }

  .marker {
    pointer-events: none;
  }
</style>
