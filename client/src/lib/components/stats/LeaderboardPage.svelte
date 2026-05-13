<script lang="ts">
  import { ArrowLeft, ChevronLeft, ChevronRight, Trophy } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import { getRankedLeaderboard, type RankedLeaderboardEntry, type GetRankedLeaderboardResult } from '../../api/client'

  interface Props {
    goBack: () => void
    direction: 'forward' | 'back'
  }

  let { goBack, direction }: Props = $props()

  let data = $state<GetRankedLeaderboardResult | null>(null)
  let loading = $state(true)
  let error = $state(false)
  let page = $state(1)
  const pageSize = 25

  $effect(() => {
    let cancelled = false
    loading = true
    error = false

    getRankedLeaderboard(page, pageSize).then((result) => {
      if (!cancelled) {
        data = result
        loading = false
      }
    }).catch(() => {
      if (!cancelled) {
        error = true
        loading = false
      }
    })

    return () => {
      cancelled = true
    }
  })

  const totalPages = $derived(data ? Math.max(1, Math.ceil(data.totalCount / pageSize)) : 1)

  function prevPage() {
    if (page > 1) page -= 1
  }

  function nextPage() {
    if (page < totalPages) page += 1
  }

  function rankColor(rank: number): string {
    if (rank === 1) return '#d79921'
    if (rank === 2) return '#a89984'
    if (rank === 3) return '#b16286'
    return 'var(--muted)'
  }
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
    <h2 class="centered-title">Leaderboard</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    {#if loading}
      <div class="message">Loading leaderboard...</div>
    {:else if error}
      <div class="message">Could not load leaderboard.</div>
    {:else if data && data.items.length > 0}
      <div class="leaderboard-list">
        {#each data.items as entry, index (entry.username)}
          {@const rank = (page - 1) * pageSize + index + 1}
          <div class="lb-row" class:lb-row--top={rank <= 3}>
            <div class="lb-rank" style="color: {rankColor(rank)}">
              {#if rank <= 3}
                <Trophy size={16} />
              {/if}
              #{rank}
            </div>
            <div class="lb-name">{entry.username}</div>
            <div class="lb-stats">
              <span class="lb-avg">{entry.averageTries.toFixed(1)}</span>
              <span class="lb-played">{entry.playedCount} played</span>
            </div>
          </div>
        {/each}
      </div>

      {#if totalPages > 1}
        <div class="pagination">
          <button class="page-btn" onclick={prevPage} disabled={page <= 1} aria-label="Previous page">
            <ChevronLeft size={18} />
          </button>
          <span class="page-info">{page} / {totalPages}</span>
          <button class="page-btn" onclick={nextPage} disabled={page >= totalPages} aria-label="Next page">
            <ChevronRight size={18} />
          </button>
        </div>
      {/if}
    {:else}
      <div class="message">No ranked players yet. Be the first!</div>
    {/if}
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
    overflow: hidden;
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
  }

  .centered-title {
    position: absolute;
    left: 50%;
    transform: translateX(-50%);
    font-size: 18px;
    font-weight: 500;
    margin: 0;
  }

  .header-spacer { width: 40px; height: 40px; }

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
    transition: background 0.2s, box-shadow 0.2s, color 0.2s;
    outline: none;
  }

  @media (hover: hover) {
    .icon-btn:hover:not(:disabled) { background: var(--hover-strong); }
  }

  .icon-btn:active:not(:disabled) { background: var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }

  .modal-body {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: 16px 20px 24px;
    display: flex;
    flex-direction: column;
    gap: 12px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar { display: none; }

  .message {
    margin: auto 0;
    text-align: center;
    color: var(--muted);
  }

  .leaderboard-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .lb-row {
    display: grid;
    grid-template-columns: 60px 1fr auto;
    gap: 12px;
    align-items: center;
    padding: 12px 14px;
    border-radius: 10px;
    background: var(--panel-soft);
    transition: background 0.15s;
  }

  .lb-row--top {
    background: color-mix(in oklab, var(--accent) 10%, var(--panel-soft));
  }

  .lb-rank {
    font-weight: 700;
    font-size: 14px;
    display: flex;
    align-items: center;
    gap: 4px;
  }

  .lb-name {
    font-weight: 600;
    font-size: 15px;
    color: var(--text);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .lb-stats {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 2px;
  }

  .lb-avg {
    font-weight: 700;
    font-size: 18px;
    color: var(--chip-bg);
    line-height: 1;
  }

  .lb-played {
    font-size: 12px;
    color: var(--muted);
  }

  .pagination {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 16px;
    padding: 8px 0;
  }

  .page-btn {
    width: 36px;
    height: 36px;
    border-radius: 50%;
    border: 1px solid var(--border);
    background: var(--panel);
    color: var(--text);
    display: grid;
    place-items: center;
    cursor: pointer;
    transition: background 0.2s;
  }

  .page-btn:disabled {
    opacity: 0.4;
    cursor: default;
  }

  @media (hover: hover) {
    .page-btn:hover:not(:disabled) { background: var(--hover-soft); }
  }

  .page-info {
    font-size: 14px;
    color: var(--muted);
    font-weight: 500;
  }
</style>
