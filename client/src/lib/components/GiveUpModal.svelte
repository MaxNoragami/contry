<script lang="ts">
  import { X, Flag } from 'lucide-svelte'
  import { fade, fly } from 'svelte/transition'

  import { APP_TIMINGS } from '../config/app'

  interface Props {
    visible: boolean
    onConfirm: () => void
    onCancel: () => void
  }

  let { visible = $bindable(false), onConfirm, onCancel }: Props = $props()

  let sessionId = $state<string | null>(null)
  let historyDepth = $state(0)

  $effect(() => {
    if (visible) {
      if (!sessionId) {
        sessionId = crypto.randomUUID()
      }
      const currentState = window.history.state
      if (currentState?.modal !== 'give-up' || currentState?.sessionId !== sessionId) {
        window.history.pushState({ modal: 'give-up', sessionId }, '')
        historyDepth++
      }
    }
  })

  function onPopState(e: PopStateEvent) {
    if (visible) {
      if (e.state?.modal === 'give-up' && e.state.sessionId === sessionId) {
        if (historyDepth > 0) historyDepth -= 1;
      } else {
        onCancel()
        setTimeout(() => {
          sessionId = null
          historyDepth = 0
        }, APP_TIMINGS.modalResetMs)
      }
    }
  }

  function close() {
    if (historyDepth > 0) {
      window.history.go(-historyDepth)
    }
    onCancel()
    setTimeout(() => {
      sessionId = null
      historyDepth = 0
    }, APP_TIMINGS.modalResetMs)
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      close()
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!visible) return
    if (e.key === 'Escape') {
      e.preventDefault()
      close()
    }
  }
</script>

<svelte:window onkeydown={handleKeydown} onpopstate={onPopState} />

{#if visible}
<!-- svelte-ignore a11y_click_events_have_key_events -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div class="modal-backdrop" onclick={handleBackdropClick} transition:fade={{duration: 200}}>
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="modal-content" onclick={e => e.stopPropagation()} in:fly={{ y: 20, duration: 250 }}>
    <div class="modal-header">
      <div style="width: 40px"></div>
      <h2>Give up?</h2>
      <button class="icon-btn" aria-label="Close" onclick={close}>
        <X />
      </button>
    </div>

    <div class="modal-body">
      <div class="modal-icon">
        <Flag size={36} />
      </div>
      <p class="modal-description">The answer will be revealed and this round will end. Ranked give-ups count as a DNF loss.</p>

      <button class="btn-confirm" onclick={onConfirm}>Give up</button>
    </div>
  </div>
</div>
{/if}

<style>
  .modal-backdrop {
    position: fixed;
    inset: 0;
    background: var(--overlay);
    backdrop-filter: blur(4px);
    z-index: 10001;
    display: flex;
    align-items: flex-end;
    justify-content: center;
  }

  @media (min-width: 768px) {
    .modal-backdrop {
      align-items: center;
    }
  }

  .modal-content {
    background: var(--panel);
    width: 100%;
    max-width: 480px;
    border-radius: 24px 24px 0 0;
    display: flex;
    flex-direction: column;
    color: var(--text);
    box-shadow: var(--shadow-lift);
    animation: slideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1);
    position: relative;
  }

  @media (min-width: 768px) {
    .modal-content {
      width: calc(100% - 32px);
      max-width: 380px;
      border-radius: 20px;
      animation: zoomIn 0.2s cubic-bezier(0.16, 1, 0.3, 1);
    }
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 20px;
  }

  .modal-body {
    padding: 0 28px 28px;
    display: flex;
    flex-direction: column;
    align-items: center;
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

  .modal-icon {
    width: 56px;
    height: 56px;
    border-radius: 50%;
    background: var(--hover-soft);
    display: grid;
    place-items: center;
    margin-bottom: 16px;
    color: var(--muted);
  }

  h2 {
    margin: 0 0 8px;
    font-size: 22px;
    font-weight: 600;
  }

  .modal-description {
    margin: 0 0 24px;
    font-size: 14px;
    color: var(--muted);
    text-align: center;
    line-height: 1.5;
  }

  .btn-confirm {
    width: 100%;
    height: 44px;
    border-radius: 12px;
    border: 1px solid color-mix(in oklab, var(--border) 74%, var(--text));
    background: color-mix(in oklab, var(--bad) 30%, var(--panel));
    color: var(--chip-bg);
    font-size: 15px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.15s, transform 0.1s, border-color 0.15s;
  }

  @media (hover: hover) {
    .btn-confirm:hover {
      background: color-mix(in oklab, var(--bad) 45%, var(--panel));
      border-color: color-mix(in oklab, var(--bad) 60%, var(--border));
    }
  }

  .btn-confirm:active {
    background: color-mix(in oklab, var(--bad) 45%, var(--panel));
    border-color: color-mix(in oklab, var(--bad) 60%, var(--border));
    transform: scale(0.96);
  }

  @keyframes slideUp {
    from { transform: translateY(100%); }
    to { transform: translateY(0); }
  }

  @keyframes zoomIn {
    from { transform: scale(0.9); opacity: 0; }
    to { transform: scale(1); opacity: 1; }
  }
</style>
