<script lang="ts">
  import { CircleDot, MonitorCog, Trash2, X } from "lucide-svelte";
  import { fade, fly } from "svelte/transition";
  import CluesSettings from "./settings/CluesSettings.svelte";
  import AddClueSettings from "./settings/AddClueSettings.svelte";
  import EditClueSettings from "./settings/EditClueSettings.svelte";
  import IconPickerSettings from "./settings/IconPickerSettings.svelte";
import DatasetEditorSettings from "./settings/DatasetEditorSettings.svelte";
import { APP_TIMINGS } from "../config/app";
import { clearAllCachedData } from "../stores/db";
  import {
    cycleThemeMode,
    getThemeModeLabel,
    getThemeModeSync,
    setThemeMode,
    type ThemeMode,
  } from "../stores/theme";

  interface Props {
    game: any;
    visible: boolean;
  }

  let { game, visible = $bindable(false) }: Props = $props();

  import type { ViewType, DraftClueData, NavDirection } from "./settings/types";

  let view: ViewType = $state("main");
  let navDirection: NavDirection = $state("forward");
  let historyDepth = $state(0);
  let sessionId = $state<string | null>(null);
  let addHasUnsavedChanges = $state(false);
  let addDiscardPromptVisible = $state(false);
  let editHasUnsavedChanges = $state(false);
  let editDiscardPromptVisible = $state(false);
  let currentThemeMode = $state<ThemeMode>(getThemeModeSync());
  let clearingCache = $state(false);

  let newClueDraft = $state<DraftClueData>({
    mode: "create",
    originalId: null,
    baselineSnapshot: null,
    id: "",
    label: "",
    description: "",
    type: "numeric",
    comparator: "higher_lower",
    unitSymbol: "",
    icon: "circle-dot",
    categories: [],
    data: [],
  });

  function resetEditGuard() {
    editHasUnsavedChanges = false;
    editDiscardPromptVisible = false;
  }

  function resetAddGuard() {
    addHasUnsavedChanges = false;
    addDiscardPromptVisible = false;
  }

  function buildModalState(targetView: ViewType) {
    return {
      modal: "settings",
      sessionId,
      view: targetView,
    };
  }

  function resetModalState() {
    view = "main";
    historyDepth = 0;
    sessionId = null;
    resetAddGuard();
    resetEditGuard();
  }

  $effect(() => {
    if (visible) {
      if (!sessionId) {
        sessionId = crypto.randomUUID();
      }

      const currentState = window.history.state;
      if (
        currentState?.modal !== "settings" ||
        currentState?.sessionId !== sessionId ||
        currentState?.view !== view
      ) {
        window.history.pushState(buildModalState(view), "");
        historyDepth++;
      }
    }
  });

  function onPopState(e: PopStateEvent) {
    if (visible) {
      if (
        e.state?.modal === "settings" &&
        e.state.sessionId === sessionId &&
        typeof e.state.view === "string"
      ) {
        const nextView = e.state.view as ViewType;
        if (view === "add-clue" && nextView !== "add-clue" && addHasUnsavedChanges) {
          window.history.pushState(buildModalState("add-clue"), "");
          historyDepth++;
          addDiscardPromptVisible = true;
          return;
        }
        if (view === "edit-clue" && nextView !== "edit-clue" && editHasUnsavedChanges) {
          window.history.pushState(buildModalState("edit-clue"), "");
          historyDepth++;
          editDiscardPromptVisible = true;
          return;
        }
        navDirection = "back";
        view = nextView;
        if (nextView !== "add-clue") {
          resetAddGuard();
        }
        if (nextView !== "edit-clue") {
          resetEditGuard();
        }
      } else {
        if (view === "add-clue" && addHasUnsavedChanges) {
          window.history.pushState(buildModalState("add-clue"), "");
          historyDepth++;
          addDiscardPromptVisible = true;
          return;
        }
        if (view === "edit-clue" && editHasUnsavedChanges) {
          window.history.pushState(buildModalState("edit-clue"), "");
          historyDepth++;
          editDiscardPromptVisible = true;
          return;
        }
        visible = false;
        setTimeout(() => {
          resetModalState();
        }, APP_TIMINGS.modalResetMs);
      }
    }
  }

  function openClues() {
    navDirection = "forward";
    view = "clues";
    resetAddGuard();
    resetEditGuard();
  }

  async function cycleTheme() {
    currentThemeMode = await setThemeMode(cycleThemeMode(currentThemeMode));
  }

  function openClearCacheWarning() {
    navDirection = "forward";
    view = "clear-cache-warning";
  }

  async function confirmClearCache() {
    if (clearingCache) return;
    clearingCache = true;
    await clearAllCachedData();
    window.location.reload();
  }

  function goBack() {
    window.history.back();
  }

  function navigateTo(newView: ViewType) {
    navDirection = "forward";
    view = newView;
    if (newView !== "add-clue") {
      resetAddGuard();
    }
    if (newView !== "edit-clue") {
      resetEditGuard();
    }
  }

  function close() {
    if (view === "add-clue" && addHasUnsavedChanges) {
      addDiscardPromptVisible = true;
      return;
    }
    if (view === "edit-clue" && editHasUnsavedChanges) {
      editDiscardPromptVisible = true;
      return;
    }
    if (view === "clear-cache-warning") {
      window.history.back();
      return;
    }
    if (historyDepth > 0) {
      window.history.go(-historyDepth);
    }
    visible = false;
    setTimeout(() => {
      resetModalState();
    }, APP_TIMINGS.modalResetMs);
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) close();
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!visible) return;
    if (e.key === "Escape") {
      e.preventDefault();
      if (view !== "main") {
        window.history.back();
      } else {
        close();
      }
    }
  }

  $effect(() => {
    if (visible) {
      currentThemeMode = getThemeModeSync();
      clearingCache = false;
    }
  });
</script>

<svelte:window onpopstate={onPopState} onkeydown={handleKeydown} />

{#if visible}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="modal-backdrop"
    onclick={handleBackdropClick}
    transition:fade={{ duration: 200 }}
  >
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="modal-content" onclick={(e) => e.stopPropagation()}>
      {#if view === "main"}
        <div
          class="view-container"
          in:fly={{ x: -20, duration: 250, delay: 100 }}
          out:fly={{ x: -20, duration: 200 }}
        >
          <div class="modal-header">
            <h2>Settings</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}>
              <X />
            </button>
          </div>
          <div class="modal-body">
            <div class="settings-list menu-actions">
              <button class="settings-item" onclick={openClues}>
                <div class="settings-item-icon">
                  <CircleDot />
                </div>
                <div class="settings-item-text">
                  <span>Clues</span>
                  <span class="muted">Customize game clues</span>
                </div>
              </button>
              <button class="settings-item" onclick={cycleTheme}>
                <div class="settings-item-icon">
                  <MonitorCog />
                </div>
                <div class="settings-item-text">
                  <span>Theme</span>
                  <span class="muted">{getThemeModeLabel(currentThemeMode)}</span>
                </div>
              </button>
              <button class="settings-item settings-item-danger" onclick={openClearCacheWarning}>
                <div class="settings-item-icon settings-item-icon-danger">
                  <Trash2 />
                </div>
                <div class="settings-item-text">
                  <span>Clear cached data</span>
                  <span class="muted">Delete stats, clues, datasets, and rounds</span>
                </div>
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if view === "clear-cache-warning"}
        <div
          class="view-container warning-view"
          in:fly={{ x: 20, duration: 250, delay: 100 }}
          out:fly={{ x: 20, duration: 200 }}
        >
          <div class="modal-header">
            <h2>Clear cached data</h2>
            <button class="icon-btn" aria-label="Close" onclick={close} disabled={clearingCache}>
              <X />
            </button>
          </div>
          <div class="warning-body">
            <div class="warning-icon">
              <Trash2 size={20} />
            </div>
            <p class="warning-text">
              This will delete all IndexedDB data, including stats, custom clues, datasets, and past rounds.
            </p>
            <div class="warning-actions">
              <button class="warning-btn muted" onclick={goBack} disabled={clearingCache}>Discard</button>
              <button class="warning-btn danger" onclick={confirmClearCache} disabled={clearingCache}>
                {clearingCache ? "Clearing..." : "Confirm"}
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if view === "clues"}
        <CluesSettings
          {game}
          onBack={goBack}
          onNavigate={navigateTo}
          direction={navDirection}
          bind:newClueDraft
        />
      {/if}

      {#if view === "add-clue"}
        <AddClueSettings
          {game}
          onBack={goBack}
          onNavigate={navigateTo}
          direction={navDirection}
          bind:hasUnsavedChanges={addHasUnsavedChanges}
          bind:discardPromptVisible={addDiscardPromptVisible}
          bind:newClueDraft
        />
      {/if}

      {#if view === "edit-clue"}
        <EditClueSettings
          {game}
          onBack={goBack}
          onNavigate={navigateTo}
          direction={navDirection}
          bind:hasUnsavedChanges={editHasUnsavedChanges}
          bind:discardPromptVisible={editDiscardPromptVisible}
          bind:newClueDraft
        />
      {/if}

      {#if view === "icon-picker"}
        <IconPickerSettings
          onBack={goBack}
          direction={navDirection}
          bind:newClueDraft
        />
      {/if}

      {#if view === "dataset-editor"}
        <DatasetEditorSettings
          {game}
          onBack={goBack}
          direction={navDirection}
          bind:newClueDraft
        />
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

  .icon-btn:disabled {
    cursor: default;
    pointer-events: none;
    box-shadow: none;
  }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 16px 0;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar {
    display: none;
  }

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

  .settings-item:last-child {
    border-bottom: none;
  }

  @media (hover: hover) {
    .settings-item:hover {
      background: var(--hover-soft);
    }
  }

  .settings-item:active {
    background: var(--hover-soft);
  }

  .settings-item-icon {
    color: var(--info);
  }

  .settings-item-icon-danger {
    color: var(--bad);
  }

  .settings-item-text {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .settings-item-text span {
    font-size: 16px;
  }

  .settings-item-text .muted {
    font-size: 13px;
    color: var(--muted);
  }

  .settings-item-danger {
    color: color-mix(in oklab, var(--text) 88%, var(--bad));
  }

  .warning-view {
    position: absolute;
    inset: 0;
    display: flex;
    flex-direction: column;
    background: var(--panel);
  }

  .warning-body {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 16px;
    padding: 24px;
    text-align: center;
  }

  .warning-icon {
    width: 52px;
    height: 52px;
    border-radius: 999px;
    display: grid;
    place-items: center;
    color: var(--bad);
    background: color-mix(in oklab, var(--bad) 12%, var(--panel));
    border: 1px solid color-mix(in oklab, var(--bad) 36%, var(--border));
  }

  .warning-text {
    margin: 0;
    max-width: 320px;
    font-size: 16px;
    line-height: 1.45;
    color: var(--text);
  }

  .warning-actions {
    display: flex;
    gap: 12px;
    width: 100%;
    max-width: 320px;
  }

  .warning-btn {
    flex: 1;
    border: none;
    border-radius: 12px;
    padding: 12px 16px;
    font-size: 15px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s, opacity 0.2s;
  }

  .warning-btn:disabled {
    cursor: default;
    opacity: 0.7;
  }

  .warning-btn.muted {
    background: var(--border);
    color: var(--text);
  }

  .warning-btn.danger {
    background: color-mix(in oklab, var(--bad) 30%, var(--panel));
    color: var(--chip-bg);
  }

  @media (hover: hover) {
    .warning-btn.muted:hover:not(:disabled) {
      background: color-mix(in oklab, var(--border) 82%, var(--text));
    }

    .warning-btn.danger:hover:not(:disabled) {
      background: color-mix(in oklab, var(--bad) 42%, var(--panel));
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
