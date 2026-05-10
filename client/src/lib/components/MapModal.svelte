<script lang="ts">
  import { slide } from 'svelte/transition'
  import { quintOut } from 'svelte/easing'
  import WorldMap from './WorldMap.svelte'

  interface Props {
    visible?: boolean
    flyTo?: { lat: number; lon: number; zoom: number } | null
    markerPosition?: { lat: number; lon: number } | null
    highlightCountry?: string | null
    guessedCountries?: string[]
    errorCountry?: string | null
    correctCountry?: string | null
    gaveUpCountry?: string | null
  }

  let {
    visible = false,
    flyTo = null,
    markerPosition = null,
    highlightCountry = null,
    guessedCountries = [],
    errorCountry = null,
    correctCountry = null,
    gaveUpCountry = null,
  }: Props = $props()
</script>

{#if visible}
  <div class="map-modal island" transition:slide={{ duration: 300, easing: quintOut }}>
    <WorldMap {flyTo} {markerPosition} {highlightCountry} {guessedCountries} {errorCountry} {correctCountry} {gaveUpCountry} />
  </div>
{/if}

<style>
  .map-modal {
    overflow: hidden;
    padding: 4px;
    width: 100%;
    aspect-ratio: 1.6;
    flex-shrink: 1;
    min-height: 120px;
    max-height: 45vh;
  }
</style>
