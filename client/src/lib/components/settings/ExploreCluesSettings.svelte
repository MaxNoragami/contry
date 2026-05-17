<script lang="ts">
  import { ArrowLeft, Binoculars, ChevronLeft, ChevronRight, Download, Pencil, Search } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import { APP_LIMITS } from '../../config/app'
  import { listCluePacks, type CluePackListItemDto } from '../../api/client'
  import type { NavDirection } from './types'

  interface Props {
    onBack: () => void
    direction: NavDirection
    onOpenCluePack: (id: string) => void | Promise<void>
  }

  let { onBack, direction, onOpenCluePack }: Props = $props()

  let query = $state('')
  let page = $state(1)
  let loading = $state(true)
  let error = $state<string | null>(null)
  let data = $state<{ items: CluePackListItemDto[]; totalCount: number; page: number; pageSize: number } | null>(null)
  let openingId = $state<string | null>(null)

  const totalPages = $derived(data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1)
  const hasSearchQuery = $derived(query.trim().length > 0)

  $effect(() => {
    let cancelled = false
    loading = true
    error = null

    listCluePacks({
      page,
      pageSize: APP_LIMITS.cluePackPageSize,
      q: query.trim() || undefined,
    }).then((result) => {
      if (cancelled) return
      data = result
      loading = false
    }).catch(() => {
      if (cancelled) return
      error = 'Could not load published clues.'
      loading = false
    })

    return () => {
      cancelled = true
    }
  })

  async function handleOpenCluePack(id: string) {
    if (openingId) return
    openingId = id
    try {
      await onOpenCluePack(id)
    } finally {
      openingId = null
    }
  }

  function prevPage() {
    if (page > 1) page -= 1
  }

  function nextPage() {
    if (page < totalPages) page += 1
  }

  function handleQueryInput(event: Event) {
    query = (event.target as HTMLInputElement).value
    page = 1
  }
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}><ArrowLeft /></button>
    <h2 class="centered-title">Explore Clues</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    <label class="search-field">
      <Search size={16} />
      <input type="text" value={query} oninput={handleQueryInput} placeholder="Search published clues..." />
    </label>

    {#if loading}
      <div class="message">Loading published clues...</div>
    {:else if error}
      <div class="message">{error}</div>
    {:else if data && data.items.length > 0}
      <div class="list menu-actions" role="list">
        {#each data.items as item (item.id)}
          <button class="clue-row" onclick={() => handleOpenCluePack(item.id)} disabled={openingId !== null}>
            <div class="clue-copy">
              <span class="clue-label">{item.label}</span>
              <span class="clue-meta">{item.ownerUsername} · {item.datasetId}</span>
            </div>
            <div class="clue-actions">
              {#if openingId === item.id}
                <span class="clue-status">Opening...</span>
              {:else if item.canEdit}
                <Pencil size={16} />
              {:else}
                <Download size={16} />
              {/if}
            </div>
          </button>
        {/each}
      </div>

      {#if totalPages > 1}
        <div class="pagination">
          <button class="page-btn" onclick={prevPage} disabled={page <= 1} aria-label="Previous page"><ChevronLeft size={18} /></button>
          <span class="page-info">{page} / {totalPages}</span>
          <button class="page-btn" onclick={nextPage} disabled={page >= totalPages} aria-label="Next page"><ChevronRight size={18} /></button>
        </div>
      {/if}
    {:else}
      <div class="empty-state">
        <div class="empty-icon"><Binoculars size={18} /></div>
        {#if hasSearchQuery}
          <p>No published clues matched your search.</p>
        {:else}
          <p>No published clues yet.</p>
          <span class="empty-subtitle">Publish the first clue pack to start building the catalog.</span>
        {/if}
      </div>
    {/if}
  </div>
</div>

<style>
  .view-container { position: absolute; inset: 0; display: flex; flex-direction: column; width: 100%; height: 100%; overflow: hidden; }
  .modal-header { display: flex; align-items: center; justify-content: space-between; padding: 16px 20px; background: var(--panel); position: relative; z-index: 2; }
  .centered-title { position: absolute; left: 50%; transform: translateX(-50%); margin: 0; font-size: 18px; font-weight: 500; }
  .header-spacer { width: 40px; height: 40px; }
  .icon-btn { width: 40px; height: 40px; border-radius: 50%; border: none; background: transparent; color: var(--text); display: grid; place-items: center; cursor: pointer; transition: background .2s, box-shadow .2s, color .2s; outline: none; }
  @media (hover:hover) { .icon-btn:hover:not(:disabled) { background: var(--hover-strong); } }
  .icon-btn:active:not(:disabled) { background: var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }
  .modal-body { flex: 1; min-height: 0; overflow-y: auto; padding: 16px; display: flex; flex-direction: column; gap: 12px; scrollbar-width: none; -ms-overflow-style: none; }
  .modal-body::-webkit-scrollbar { display: none; }
  .search-field { display: grid; grid-template-columns: 16px 1fr; align-items: center; gap: 10px; padding: 0 12px; min-height: 42px; border: 1px solid var(--border); border-radius: 10px; background: var(--panel-soft); color: var(--muted); }
  .search-field input { width: 100%; border: none; background: transparent; color: var(--text); outline: none; font: inherit; }
  .menu-actions { display: flex; flex-direction: column; background: var(--panel); border-radius: 12px; overflow: hidden; border: 1px solid var(--border); }
  .clue-row { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: 12px; width: 100%; padding: 14px 16px; border: none; border-bottom: 1px solid var(--border); background: transparent; color: var(--text); text-align: left; cursor: pointer; transition: background .15s; }
  .clue-row:last-child { border-bottom: none; }
  @media (hover:hover) { .clue-row:hover:not(:disabled) { background: var(--hover-soft); } }
  .clue-row:active:not(:disabled) { background: var(--hover-soft); }
  .clue-row:disabled { cursor: default; }
  .clue-copy { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
  .clue-label { font-size: 15px; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .clue-meta { font-size: 12px; color: var(--muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .clue-actions { color: var(--muted); display: flex; align-items: center; }
  .clue-status { font-size: 12px; }
  .message { margin: auto 0; text-align: center; color: var(--muted); }
  .empty-state { margin: auto 0; display: flex; flex-direction: column; align-items: center; gap: 12px; text-align: center; color: var(--muted); }
  .empty-icon { width: 40px; height: 40px; display: grid; place-items: center; border-radius: 10px; background: var(--panel-soft); border: 1px solid var(--border); }
  .empty-subtitle { max-width: 260px; font-size: 13px; line-height: 1.45; }
  .pagination { display: flex; align-items: center; justify-content: center; gap: 16px; }
  .page-btn { width: 36px; height: 36px; border-radius: 50%; border: 1px solid var(--border); background: var(--panel); color: var(--text); display: grid; place-items: center; cursor: pointer; }
  .page-btn:disabled { opacity: .4; cursor: default; }
  .page-info { color: var(--muted); font-size: 13px; }
</style>
