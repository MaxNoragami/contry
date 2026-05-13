<script lang="ts">
  import {
    CircleQuestionMark,
    Joystick,
    Settings,
    Swords,
    UserRound,
    UserRoundPlus,
  } from 'lucide-svelte'

  import type { GameMode } from '../stores/game-mode.svelte'

  interface Props {
    onSettingsClick?: () => void
    onHelpClick?: () => void
    onProfileClick?: () => void
    onModeToggle?: () => void
    mode: GameMode
    isAuthenticated: boolean
    settingsDisabled?: boolean
  }
  let {
    onSettingsClick,
    onHelpClick,
    onProfileClick,
    onModeToggle,
    mode,
    isAuthenticated,
    settingsDisabled = false,
  }: Props = $props()
</script>

<header class="island header-island">
  <div class="header-actions">
    <button
      type="button"
      class="icon-btn"
      aria-label={mode === 'ranked' ? 'Switch to arcade mode' : 'Switch to ranked mode'}
      onclick={onModeToggle}
    >
      {#if mode === 'ranked'}
        <Joystick />
      {:else}
        <Swords />
      {/if}
    </button>
    <button type="button" class="icon-btn" aria-label="Help" onclick={onHelpClick}><CircleQuestionMark /></button>
  </div>
  <h1>CŌNTRY</h1>
  <div class="header-actions header-actions--end">
    <button type="button" class="icon-btn" aria-label={isAuthenticated ? 'User profile' : 'Account'} onclick={onProfileClick}>
      {#if isAuthenticated}
        <UserRound />
      {:else}
        <UserRoundPlus />
      {/if}
    </button>
    <button type="button" class="icon-btn" aria-label="Settings" onclick={onSettingsClick} disabled={settingsDisabled}><Settings /></button>
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

  .icon-btn:disabled {
    opacity: 0.45;
    cursor: default;
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
