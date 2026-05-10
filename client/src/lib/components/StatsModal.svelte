<script lang="ts">
  import { Globe, X, ChartColumnBig, CircleDot } from 'lucide-svelte'
  import { fade, fly } from 'svelte/transition'
  import DiscoveryStatsPage from './stats/DiscoveryStatsPage.svelte'
  import DistributionStatsPage from './stats/DistributionStatsPage.svelte'
  import ClueUsageStatsPage from './stats/ClueUsageStatsPage.svelte'

  type StatsView = 'main' | 'discovery' | 'distributions' | 'clues'

  interface Props {
    game: any
    visible: boolean
  }

  let { game, visible = $bindable(false) }: Props = $props()

  let view: StatsView = $state('main')
  let direction = $state<'forward' | 'back'>('forward')
  let historyDepth = $state(0)
  let sessionId = $state<string | null>(null)

  function buildModalState(targetView: StatsView) {
    return {
      modal: 'stats',
      sessionId,
      view: targetView,
    }
  }

  function resetModalState() {
    view = 'main'
    historyDepth = 0
    sessionId = null
  }

  $effect(() => {
    if (visible) {
      if (!sessionId) {
        sessionId = crypto.randomUUID()
      }

      const currentState = window.history.state
      if (
        currentState?.modal !== 'stats' ||
        currentState?.sessionId !== sessionId ||
        currentState?.view !== view
      ) {
        window.history.pushState(buildModalState(view), '')
        historyDepth++
      }
    }
  })

  function onPopState(e: PopStateEvent) {
    if (!visible) return

    if (
      e.state?.modal === 'stats' &&
      e.state.sessionId === sessionId &&
      typeof e.state.view === 'string'
    ) {
      direction = 'back'
      view = e.state.view as StatsView
    } else {
      visible = false
      setTimeout(() => {
        resetModalState()
      }, 300)
    }
  }

  function close() {
    if (historyDepth > 0) {
      window.history.go(-historyDepth)
    }
    visible = false
    setTimeout(() => {
      resetModalState()
    }, 300)
  }

  function openDiscovery() {
    direction = 'forward'
    view = 'discovery'
  }

  function openDistributions() {
    direction = 'forward'
    view = 'distributions'
  }

  function openClues() {
    direction = 'forward'
    view = 'clues'
  }

  function goBack() {
    window.history.back()
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) close()
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!visible) return
    if (e.key === 'Escape') {
      e.preventDefault()
      if (view !== 'main') window.history.back()
      else close()
    }
  }
</script>

<svelte:window onpopstate={onPopState} onkeydown={handleKeydown} />

{#if visible}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="modal-backdrop" onclick={handleBackdropClick} transition:fade={{ duration: 200 }}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="modal-content" onclick={(e) => e.stopPropagation()}>
      {#if view === 'main'}
        <div class="view-container" in:fly={{ x: -20, duration: 250, delay: 100 }} out:fly={{ x: -20, duration: 200 }}>
          <div class="modal-header">
            <h2>Statistics</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}><X /></button>
          </div>

          <div class="modal-body">
            <div class="settings-list menu-actions">
              <button class="settings-item" onclick={openDiscovery}>
                <div class="settings-item-icon"><Globe /></div>
                <div class="settings-item-text">
                  <span>Cōntry discovery</span>
                  <span class="muted">See which countries you've already solved</span>
                </div>
              </button>

              <button class="settings-item" onclick={openDistributions}>
                <div class="settings-item-icon"><ChartColumnBig /></div>
                <div class="settings-item-text">
                  <span>Distributions</span>
                  <span class="muted">Review guess patterns and the countries you give up on most</span>
                </div>
              </button>

              <button class="settings-item" onclick={openClues}>
                <div class="settings-item-icon"><CircleDot /></div>
                <div class="settings-item-text">
                  <span>Clues</span>
                  <span class="muted">See which clues show up most often in your games</span>
                </div>
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if view === 'discovery'}
        <DiscoveryStatsPage {game} {goBack} {direction} />
      {/if}

      {#if view === 'distributions'}
        <DistributionStatsPage {game} {goBack} {direction} />
      {/if}

      {#if view === 'clues'}
        <ClueUsageStatsPage {game} {goBack} {direction} />
      {/if}
    </div>
  </div>
{/if}

<style>
  .modal-backdrop {
    position: fixed;
    inset: 0;
    background: var(--overlay);
    backdrop-filter: blur(4px);
    z-index: 10000;
    display: flex;
    align-items: flex-end;
    justify-content: center;
  }

  @media (min-width: 768px) {
    .modal-backdrop { align-items: center; }
  }

  .modal-content {
    background: var(--panel);
    width: 100%;
    max-width: 480px;
    height: 90vh;
    border-radius: 24px 24px 0 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    color: var(--text);
    box-shadow: var(--shadow-strong);
    position: relative;
  }

  @media (min-width: 768px) {
    .modal-content {
      height: 80vh;
      max-height: 700px;
      border-radius: 20px;
    }
  }

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
    justify-content: space-between;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
  }

  .modal-header h2 {
    margin: 0;
    font-size: 20px;
    font-weight: 500;
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
    overflow-y: auto;
    padding: 16px 0;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar { display: none; }

  .settings-list {
    display: flex;
    flex-direction: column;
  }

  .menu-actions {
    background: var(--panel);
    border-radius: 12px;
    overflow: hidden;
    margin: 0 16px;
    border: 1px solid var(--border);
  }

  .settings-item {
    display: flex;
    align-items: center;
    gap: 20px;
    padding: 16px 24px;
    width: 100%;
    background: transparent;
    border: none;
    color: var(--text);
    text-align: left;
    cursor: pointer;
    transition: background 0.2s;
    border-bottom: 1px solid var(--border);
  }

  .settings-item:last-child { border-bottom: none; }

  @media (hover: hover) {
    .settings-item:hover { background: var(--hover-soft); }
  }

  .settings-item:active { background: var(--hover-soft); }

  .settings-item-icon { color: var(--info); }
  .settings-item-text { display: flex; flex-direction: column; gap: 4px; }
  .settings-item-text span { font-size: 16px; }
  .settings-item-text .muted { font-size: 13px; color: var(--muted); }
</style>
