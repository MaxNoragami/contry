<script lang="ts">
  import { ArrowLeft } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import type { DraftClueData, NavDirection } from './types'

  interface Props {
    game: any
    onBack: () => void
    direction: NavDirection
    newClueDraft: DraftClueData
  }

  let { game, onBack, direction, newClueDraft = $bindable() }: Props = $props()

  let searchQuery = $state('')

  // Build a fast lookup map for country names
  const countryMap = $derived.by(() => {
    const map = new Map<string, string>()
    for (const c of game.countryPool) {
      map.set(c.country_id, c.name)
    }
    return map
  })

  // Bindable wrapper for the array to allow inline editing while filtering
  const tableRows = $derived.by(() => {
    let result = newClueDraft.data.map((row, index) => {
      const name = countryMap.get(row.country_id) || row.country_id
      return { index, country_id: row.country_id, name, value: row.value }
    })
    
    const q = searchQuery.toLowerCase().trim()
    if (q) {
      result = result.filter(r => r.name.toLowerCase().includes(q) || String(r.value).toLowerCase().includes(q))
    }
    
    // Sort alphabetically by country name
    result.sort((a, b) => a.name.localeCompare(b.name))
    return result
  })

  function handleInput(index: number, e: Event) {
    if (newClueDraft.readOnly) return
    const val = (e.target as HTMLInputElement).value
    const trimmed = val.trim()

    const nextRows = [...newClueDraft.data]
    const nextRow = { ...nextRows[index] }

    if (trimmed === '') {
      nextRow.value = null
      nextRows[index] = nextRow
      newClueDraft.data = nextRows
      return
    }

    const num = Number(trimmed)
    nextRow.value = Number.isNaN(num) ? val : num
    nextRows[index] = nextRow
    newClueDraft.data = nextRows
  }
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}>
      <ArrowLeft />
    </button>
    <div class="search-container">
      <input 
        type="text" 
        bind:value={searchQuery} 
        placeholder="Filter countries..." 
      />
    </div>
  </div>
  
  <div class="modal-body">
    <div class="table-container">
      <table>
        <thead>
          <tr>
            <th>Country</th>
            <th>{newClueDraft.label || 'Value'}</th>
          </tr>
        </thead>
        <tbody>
          {#each tableRows as row (row.country_id)}
            <tr>
              <td class="country-cell" title={row.country_id}>{row.name}</td>
              <td class="value-cell">
                <input 
                  type="text" 
                  value={row.value ?? ''} 
                  disabled={newClueDraft.readOnly}
                  oninput={(e) => handleInput(row.index, e)} 
                />
              </td>
            </tr>
          {/each}
          {#if tableRows.length === 0}
            <tr>
              <td colspan="2" class="empty-state">No data matches your filter.</td>
            </tr>
          {/if}
        </tbody>
      </table>
    </div>
  </div>
</div>

<style>
  .view-container {
    position: absolute;
    inset: 0;
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
  }

  .modal-header {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
  }

  .icon-btn {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    border: none;
    background: transparent;
    color: var(--text);
    display: grid;
    place-items: center;
    cursor: pointer;
    flex-shrink: 0;
    transition: background 0.2s, box-shadow 0.2s, color 0.2s;
    outline: none;
  }

  @media (hover: hover) {
    .icon-btn:hover:not(:disabled) {
      background: var(--hover-strong);
    }
  }

  .icon-btn:active:not(:disabled) {
    background: var(--hover-strong);
  }

  .icon-btn:focus-visible:not(:disabled) {
    box-shadow: inset 0 0 0 1px var(--info);
  }

  .search-container {
    flex: 1;
    position: relative;
    display: flex;
    align-items: center;
  }

  .search-container input {
    width: 100%;
    background: var(--field-bg);
    border: none;
    padding: 10px;
    border-radius: 8px;
    color: var(--text);
    font-size: 15px;
    outline: none;
  }

  .search-container input:focus {
    box-shadow: inset 0 0 0 1px var(--info);
  }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 0;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }
  .modal-body::-webkit-scrollbar {
    display: none;
  }

  .table-container {
    width: 100%;
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th {
    position: sticky;
    top: 0;
    background: var(--panel);
    color: var(--muted);
    font-weight: 500;
    font-size: 14px;
    text-align: left;
    padding: 12px 20px;
    border-bottom: 1px solid var(--border);
    z-index: 1;
  }

  td {
    padding: 0;
    border-bottom: 1px solid var(--border);
  }

  .country-cell {
    padding: 12px 20px;
    font-size: 15px;
    color: var(--text);
    width: 50%;
  }

  .value-cell {
    width: 50%;
  }

  .value-cell input {
    width: 100%;
    height: 100%;
    padding: 12px 20px;
    background: transparent;
    border: none;
    color: var(--info);
    font-size: 15px;
    outline: none;
  }

  .value-cell input:focus {
    background: var(--field-focus-bg);
  }

  .empty-state {
    padding: 40px 20px;
    text-align: center;
    color: var(--muted);
  }
</style>
