<script lang="ts">
  import { ArrowLeft } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import { API_PATHS, getLucideIconUrl } from '../../config/app'
  import { type ClueUsageStatDto, type MyRankedStatsResult } from '../../api/client'
  import type { createAuthStore } from '../../stores/auth.svelte'
  import { iconMap } from '../../stores/game.svelte'

  interface Props {
    auth: ReturnType<typeof createAuthStore>
    goBack: () => void
    direction: 'forward' | 'back'
  }

  let { auth, goBack, direction }: Props = $props()

  const clueDisplayInfo: Record<string, { label: string; icon?: string }> = {
    hemisphere: { label: 'Hemisphere', icon: 'globe' },
    continent: { label: 'Continent', icon: 'compass' },
    temperature_avg_c: { label: 'Avg. Temperature', icon: 'thermometer' },
    population: { label: 'Population', icon: 'users' },
    coordinates: { label: 'Coordinates', icon: 'navigation' },
    area_km2: { label: 'Area', icon: 'land-plot' },
  }

  type ClueUsageEntry = {
    id: string
    label: string
    icon: typeof import('lucide-svelte').Icon | null
    customIcon?: string
    usage_count: number
  }

  let clues = $state<ClueUsageEntry[]>([])
  let loading = $state(true)
  let error = $state(false)

  $effect(() => {
    let cancelled = false
    loading = true

    loadClueUsageStats().then((data) => {
      if (!cancelled) {
        clues = data
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

  async function loadClueUsageStats(): Promise<ClueUsageEntry[]> {
    const apiStats = await auth.request<MyRankedStatsResult>(API_PATHS.ranked.statsMe)

    return apiStats.clueUsageStats
      .map((stat: ClueUsageStatDto) => {
        const info = clueDisplayInfo[stat.clueId]
        const iconKey = info?.icon ?? stat.clueId
        return {
          id: stat.clueId,
          label: info?.label ?? stat.clueId,
          icon: iconMap[iconKey] ?? null,
          customIcon: !iconMap[iconKey] ? iconKey : undefined,
          usage_count: stat.usageCount,
        }
      })
      .sort((a: ClueUsageEntry, b: ClueUsageEntry) => {
        if (b.usage_count !== a.usage_count) return b.usage_count - a.usage_count
        return a.label.localeCompare(b.label)
      })
  }

  function maxUsage() {
    if (clues.length === 0) return 1
    return Math.max(1, ...clues.map((c) => c.usage_count))
  }
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
    <h2 class="centered-title">Clues</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    {#if loading}
      <div class="message">Loading clue stats...</div>
    {:else if error}
      <div class="message">Could not load clue stats.</div>
    {:else if clues.length > 0}
      <div class="clue-list">
        {#each clues as clue (clue.id)}
          <div class="clue-row">
            <div class="clue-leading">
              <div class="clue-icon-wrapper">
                {#if clue.icon}
                  {@const IconComponent = clue.icon}
                  <IconComponent size={20} />
                {:else if clue.customIcon}
                  <div class="custom-icon" style={`mask-image: url('${getLucideIconUrl(clue.customIcon)}'); -webkit-mask-image: url('${getLucideIconUrl(clue.customIcon)}');`}></div>
                {/if}
              </div>
              <div class="clue-name">{clue.label}</div>
            </div>

            <div class="clue-bar-wrap">
              {#if clue.usage_count > 0}
                <div class="clue-bar" style={`width:${(clue.usage_count / maxUsage()) * 100}%`}>
                  <span class="clue-count">{clue.usage_count}</span>
                </div>
              {:else}
                <div class="clue-bar clue-bar--empty"><span class="clue-count clue-count--muted">0</span></div>
              {/if}
            </div>
          </div>
        {/each}
      </div>
    {:else}
      <div class="message">No clue usage recorded yet.</div>
    {/if}
  </div>
</div>

<style>
  .view-container { position: absolute; inset: 0; display: flex; flex-direction: column; width: 100%; height: 100%; overflow: hidden; }
  .modal-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; background:var(--panel); position:relative; z-index:2; }
  .centered-title { position:absolute; left:50%; transform:translateX(-50%); font-size:18px; font-weight:500; margin:0; }
  .header-spacer { width:40px; height:40px; }
  .icon-btn { width:40px; height:40px; border-radius:50%; border:none; background:transparent; color:var(--text); display:grid; place-items:center; cursor:pointer; transition:background .2s, box-shadow .2s, color .2s; outline:none; }
  @media (hover:hover) { .icon-btn:hover:not(:disabled) { background:var(--hover-strong); } }
  .icon-btn:active:not(:disabled) { background:var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }
  .modal-body { flex:1; min-height:0; overflow-y:auto; padding:16px 20px 24px; scrollbar-width:none; -ms-overflow-style:none; }
  .modal-body::-webkit-scrollbar { display:none; }
  .message { margin:auto 0; text-align:center; color:var(--muted); }
  .clue-list { display:flex; flex-direction:column; gap:16px; }
  .clue-row { display:grid; grid-template-columns: 92px 1fr; gap:16px; align-items:center; }
  .clue-leading { display:flex; flex-direction:column; align-items:center; gap:8px; text-align:center; }
  .clue-icon-wrapper { width:40px; height:40px; border-radius:50%; background:var(--chip-bg); color:var(--chip-fg); display:grid; place-items:center; }
  .clue-name { font-size:13px; font-weight:600; line-height:1.2; }
  .custom-icon { width:20px; height:20px; background-color:currentColor; mask-size:contain; mask-repeat:no-repeat; mask-position:center; -webkit-mask-size:contain; -webkit-mask-repeat:no-repeat; -webkit-mask-position:center; }
  .clue-bar-wrap { min-height:24px; display:flex; align-items:center; }
  .clue-bar { min-height:24px; min-width:28px; border-radius:999px; background:var(--info); display:flex; align-items:center; justify-content:flex-end; padding:0 8px; }
  .clue-bar--empty { background:var(--ring-track); }
  .clue-count { font-size:13px; font-weight:700; color:var(--chip-fg); }
  .clue-count--muted { color:var(--muted); }
  @media (max-width: 520px) { .clue-row { grid-template-columns: 72px 1fr; gap:12px; } }
</style>
