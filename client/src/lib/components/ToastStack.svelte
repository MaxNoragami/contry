<script lang="ts">
  import { fade, fly } from 'svelte/transition'
  import { toastStore } from '../stores/toasts.svelte'
</script>

<div class="toast-stack" aria-live="polite" aria-atomic="false">
  {#each toastStore.items as toast (toast.id)}
    <div class="toast {toast.tone}" in:fly={{ y: -10, duration: 180 }} out:fade={{ duration: 180 }}>
      {toast.message}
    </div>
  {/each}
</div>

<style>
  .toast-stack {
    position: fixed;
    top: 8px;
    left: 50%;
    transform: translateX(-50%);
    z-index: 50000;
    width: min(640px, calc(100vw - 20px));
    min-width: min(320px, calc(100vw - 20px));
    display: flex;
    flex-direction: column;
    gap: 8px;
    pointer-events: none;
  }

  @media (min-width: 768px) {
    .toast-stack {
      top: 10px;
      width: min(680px, calc(100vw - 28px));
      min-width: 420px;
    }
  }

  .toast {
    border-radius: 12px;
    border: 1px solid var(--toast-error-border);
    background: var(--toast-error-bg);
    color: var(--toast-error-text);
    padding: 12px 16px;
    line-height: 1.35;
    box-shadow: 0 10px 28px rgba(0, 0, 0, 0.45);
    font-size: clamp(14px, 1.4vw, 17px);
    pointer-events: none;
    backdrop-filter: none;
    opacity: 1;
    overflow-wrap: break-word;
    text-align: center;
  }
</style>
