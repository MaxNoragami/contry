<script lang="ts">
  import {
    ChartColumnBig,
    CircleQuestionMark,
    Heart,
    Settings,
  } from 'lucide-svelte'

  interface Props {
    onSettingsClick?: () => void
    onHelpClick?: () => void
    onStatsClick?: () => void
  }
  let { onSettingsClick, onHelpClick, onStatsClick }: Props = $props()
</script>

<header class="island header-island">
  <div class="header-actions">
    <button type="button" class="icon-btn" aria-label="Favorites"><Heart /></button>
    <button type="button" class="icon-btn" aria-label="Help" onclick={onHelpClick}><CircleQuestionMark /></button>
  </div>
  <h1>CŌNTRY</h1>
  <div class="header-actions header-actions--end">
    <button type="button" class="icon-btn" aria-label="Stats" onclick={onStatsClick}><ChartColumnBig /></button>
    <button type="button" class="icon-btn" aria-label="Settings" onclick={onSettingsClick}><Settings /></button>
  </div>
</header>

<style>
  .header-island {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    padding: 0 12px;
    height: var(--island-row-h, 54px);
  }

  h1 {
    margin: 0;
    font-size: clamp(14px, 2vw, 26px);
    font-weight: 700;
    letter-spacing: 0.09em;
    white-space: nowrap;
    text-align: center;
    overflow: hidden;
    text-overflow: ellipsis;
    min-width: 0;
  }

  .header-actions {
    display: flex;
    gap: 4px;
  }

  .header-actions--end {
    justify-content: flex-end;
  }

  .icon-btn {
    width: 30px;
    height: 30px;
    border-radius: 8px;
    border: 1px solid transparent;
    background: transparent;
    color: var(--muted);
    display: grid;
    place-items: center;
    cursor: pointer;
    transition: border-color 0.15s, background 0.15s, color 0.15s;
    flex-shrink: 0;
  }

  .icon-btn:focus-visible {
    border-color: var(--border-strong);
    background: var(--panel-2);
    color: var(--text);
  }

  @media (hover: hover) {
    .icon-btn:hover {
      border-color: var(--border-strong);
      background: var(--panel-2);
      color: var(--text);
    }
  }

  .icon-btn:active {
    border-color: var(--border-strong);
    background: var(--panel-2);
    color: var(--text);
  }

  :global(.icon-btn svg) {
    width: 16px;
    height: 16px;
  }
</style>
