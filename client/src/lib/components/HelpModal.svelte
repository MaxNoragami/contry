<script lang="ts">
  import { X } from 'lucide-svelte'
  import { fade } from 'svelte/transition'
  import { getLucideIconUrl } from '../config/app'

  interface Props {
    visible: boolean
    game: any
    onClose: () => void
  }

  let { visible = $bindable(false), game, onClose }: Props = $props()

  const builtInClues = $derived.by(() =>
    game.availableClues.filter((clue: any) => clue.source !== 'custom')
  )

  const customClues = $derived.by(() =>
    game.availableClues.filter((clue: any) => clue.source === 'custom')
  )

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      onClose()
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!visible) return
    if (e.key === 'Escape') {
      e.preventDefault()
      onClose()
    }
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if visible}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="modal-backdrop" onclick={handleBackdropClick} transition:fade={{ duration: 200 }}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="modal-content" onclick={(e) => e.stopPropagation()}>
      <div class="modal-header">
        <h2>Help</h2>
        <button class="icon-btn" aria-label="Close" onclick={onClose}><X size={20} /></button>
      </div>

      <div class="modal-body">
        <p class="mission-text">Find the hidden country by comparing each guess against the clue feedback. Use what changes after every attempt to narrow the search and solve it in as few guesses as possible.</p>

        <div class="section-divider"></div>

        <section class="section-block">
          <h3>The clues</h3>
          <div class="clue-list">
            {#each builtInClues as clue (clue.id)}
              <div class="clue-row">
                <div class="clue-icon-wrapper">
                  {#if clue.icon}
                    {@const IconComponent = clue.icon}
                    <IconComponent size={20} />
                  {:else if clue.customIcon}
                    <div class="custom-icon" style={`mask-image: url('${getLucideIconUrl(clue.customIcon)}'); -webkit-mask-image: url('${getLucideIconUrl(clue.customIcon)}');`}></div>
                  {/if}
                </div>
                <div class="clue-copy">
                  <div class="clue-name">{clue.label}</div>
                  <p class="clue-description">{clue.description || 'No description provided yet.'}</p>
                </div>
              </div>
            {/each}
          </div>
        </section>

        <section class="section-block">
          <h3>Custom clues</h3>
          {#if customClues.length > 0}
            <div class="clue-list">
              {#each customClues as clue (clue.id)}
                <div class="clue-row">
                  <div class="clue-icon-wrapper">
                    {#if clue.icon}
                      {@const IconComponent = clue.icon}
                      <IconComponent size={20} />
                    {:else if clue.customIcon}
                      <div class="custom-icon" style={`mask-image: url('${getLucideIconUrl(clue.customIcon)}'); -webkit-mask-image: url('${getLucideIconUrl(clue.customIcon)}');`}></div>
                    {/if}
                  </div>
                  <div class="clue-copy">
                    <div class="clue-name custom-name">{clue.label}</div>
                    <p class="clue-description">{clue.description || 'No description provided yet.'}</p>
                  </div>
                </div>
              {/each}
            </div>
          {:else}
            <p class="empty-text">You have not created any custom clues yet.</p>
          {/if}
        </section>

        <div class="section-divider"></div>

        <section class="section-block">
          <h3>Reading the feedback</h3>
          <div class="legend-list">
            <div class="legend-row">
              <div class="legend-dot legend-dot--red"></div>
              <p class="legend-text">Red means this clue is far from the hidden country.</p>
            </div>
            <div class="legend-row">
              <div class="legend-dot legend-dot--yellow"></div>
              <p class="legend-text">Yellow means you are getting closer, but it is not an exact match yet.</p>
            </div>
            <div class="legend-row">
              <div class="legend-dot legend-dot--green"></div>
              <p class="legend-text">Green means that clue matches the hidden country.</p>
            </div>
          </div>


          <div class="legend-list legend-list--arrows">
            <div class="legend-row">
              <div class="legend-arrow">▲</div>
              <p class="legend-text">An up arrow means the hidden country's value is higher.</p>
            </div>
            <div class="legend-row">
              <div class="legend-arrow">▼</div>
              <p class="legend-text">A down arrow means the hidden country's value is lower.</p>
            </div>
          </div>
        </section>
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
    height: 90vh;
    border-radius: 24px 24px 0 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    color: var(--text);
    box-shadow: var(--shadow-strong);
    animation: slideUp 0.3s cubic-bezier(0.16, 1, 0.3, 1);
    position: relative;
  }

  @media (min-width: 768px) {
    .modal-content {
      height: 80vh;
      max-height: 700px;
      border-radius: 20px;
      animation: zoomIn 0.2s cubic-bezier(0.16, 1, 0.3, 1);
    }
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 20px;
    background: var(--panel);
    position: sticky;
    top: 0;
    z-index: 2;
  }

  .modal-header h2 {
    margin: 0;
    font-size: 20px;
    font-weight: 600;
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
    .icon-btn:hover {
      background: var(--hover-strong);
    }
  }

  .icon-btn:active {
    background: var(--hover-strong);
  }

  .icon-btn:focus-visible {
    box-shadow: inset 0 0 0 1px var(--info);
  }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    display: flex;
    flex-direction: column;
    gap: 28px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar {
    display: none;
  }

  .mission-text,
  .empty-text {
    margin: 0;
    font-size: 15px;
    line-height: 1.6;
    color: var(--text);
  }

  .empty-text {
    color: var(--muted);
  }

  .section-block {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .section-block h3 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
  }

  .section-divider {
    width: 100%;
    height: 0;
    border-top: 1px solid var(--border-strong);
    opacity: 0.9;
  }

  .legend-list {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .legend-row {
    display: grid;
    grid-template-columns: 40px 1fr;
    column-gap: 16px;
    align-items: center;
  }

  .legend-dot {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    justify-self: center;
    box-shadow: inset -8px -8px 0 var(--inset-dark);
  }

  .legend-dot--red {
    background: var(--bad);
  }

  .legend-dot--yellow {
    background: color-mix(in oklab, var(--warn) 86%, var(--chip-bg));
  }

  .legend-dot--green {
    background: var(--ok);
  }

  .legend-arrow {
    justify-self: center;
    font-size: 20px;
    font-weight: 700;
    color: var(--chip-bg);
  }

  .legend-text {
    margin: 0;
    font-size: 15px;
    line-height: 1.5;
    color: color-mix(in oklab, var(--text) 82%, var(--muted));
  }

  .clue-list {
    display: flex;
    flex-direction: column;
    gap: 14px;
  }

  .clue-row {
    display: grid;
    grid-template-columns: 52px 1fr;
    column-gap: 16px;
    align-items: start;
  }

  .clue-icon-wrapper {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    background: var(--chip-bg);
    color: var(--chip-fg);
    display: grid;
    place-items: center;
    flex-shrink: 0;
  }

  .clue-name {
    font-size: 14px;
    font-weight: 600;
    line-height: 1.2;
    text-align: left;
  }

  .clue-copy {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 6px;
    padding-top: 0;
    width: 100%;
    text-align: left;
  }

  .custom-name {
    font-style: italic;
  }

  .clue-description {
    margin: 0;
    font-size: 15px;
    line-height: 1.5;
    color: color-mix(in oklab, var(--text) 82%, var(--muted));
    font-style: italic;
  }

  .custom-icon {
    width: 20px;
    height: 20px;
    background-color: currentColor;
    mask-size: contain;
    mask-repeat: no-repeat;
    mask-position: center;
    -webkit-mask-size: contain;
    -webkit-mask-repeat: no-repeat;
    -webkit-mask-position: center;
  }

  @media (max-width: 640px) {
    .clue-row {
      grid-template-columns: 40px 1fr;
      column-gap: 14px;
      align-items: start;
    }
  }

  @keyframes slideUp {
    from {
      transform: translateY(100%);
    }
    to {
      transform: translateY(0);
    }
  }

  @keyframes zoomIn {
    from {
      transform: scale(0.95);
      opacity: 0;
    }
    to {
      transform: scale(1);
      opacity: 1;
    }
  }
</style>
