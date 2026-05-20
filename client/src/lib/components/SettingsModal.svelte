<script lang="ts">
  import { CircleDot, MonitorCog, Shield, Trophy, Trash2, X } from "lucide-svelte";
  import { fade, fly } from "svelte/transition";
  import AdminPanelSettings from "./settings/AdminPanelSettings.svelte";
  import AdminRankedRoundSettings from "./settings/AdminRankedRoundSettings.svelte";
  import CluesSettings from "./settings/CluesSettings.svelte";
  import AddClueSettings from "./settings/AddClueSettings.svelte";
  import EditClueSettings from "./settings/EditClueSettings.svelte";
  import ExploreCluesSettings from "./settings/ExploreCluesSettings.svelte";
  import ViewClueSettings from "./settings/ViewClueSettings.svelte";
  import IconPickerSettings from "./settings/IconPickerSettings.svelte";
import DatasetEditorSettings from "./settings/DatasetEditorSettings.svelte";
import { APP_TIMINGS } from "../config/app";
import { DEFAULT_CLUE_IDS } from "../config/app";
import { API_PATHS } from "../config/app";
import { getCluePack } from "../api/client";
import { canPushCloudLink, getCloudDetailFetcher, importPublishedClueToLocal, syncWorkspaceLinkedClues } from "../clues/cloud";
import { loadWorkspaceCloudLinks, loadWorkspaceCustomClues, loadWorkspaceCustomRows, removeWorkspaceCustomRowsForClue, saveWorkspaceCloudLinks, saveWorkspaceCustomClues, saveWorkspaceSelectedClues } from "../clues/workspace";
import { clearAllCachedData } from "../stores/db";
import { getDB } from "../stores/db";
import { toastStore } from "../stores/toasts.svelte";
  import {
    cycleThemeMode,
    getThemeModeLabel,
    getThemeModeSync,
    setThemeMode,
    type ThemeMode,
  } from "../stores/theme";
  import type { GameMode } from "../stores/game-mode.svelte";
  import type { createAuthStore } from "../stores/auth.svelte";

  interface Props {
    game: any;
    auth: ReturnType<typeof createAuthStore>;
    mode: GameMode;
    visible: boolean;
  }

  let { game, auth, mode, visible = $bindable(false) }: Props = $props();

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
  let resettingLeaderboard = $state(false);

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
      remoteId: null,
      ownerId: null,
      ownerUsername: null,
      visibility: null,
      readOnly: false,
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
        if (historyDepth > 0) historyDepth -= 1;
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

  async function openClues() {
    if (mode === 'ranked' && auth.user?.role !== 'ADMIN') {
      return;
    }
    const db = await getDB()
    await syncWorkspaceLinkedClues(db, getCloudDetailFetcher(auth), { force: true })
    await game.refreshCustomClueCatalog(false)
    navDirection = "forward";
    view = "clues";
    resetAddGuard();
    resetEditGuard();
  }

  async function openLocalCustomClue(clueId: string) {
    const db = await getDB()
    await syncWorkspaceLinkedClues(db, getCloudDetailFetcher(auth), { force: true })
    await game.refreshCustomClueCatalog(false)
    const customClues = await loadWorkspaceCustomClues(db) as any[]
    const cloudLinks = await loadWorkspaceCloudLinks(db)
    const cloudLink = cloudLinks[clueId] || null
    const customMetadata = customClues.find((c) => c.id === clueId && c.source === 'custom')
    if (!customMetadata) return

    const rowsMap = await loadWorkspaceCustomRows(db)
    const rows = rowsMap[clueId] || []

    const loadedDraft: DraftClueData = {
      mode: cloudLink && !canPushCloudLink(cloudLink, auth.user?.id, auth.user?.role) ? 'view' : 'edit',
      originalId: customMetadata.id,
      baselineSnapshot: null,
      id: customMetadata.id,
      label: customMetadata.label || customMetadata.id,
      description: customMetadata.description || '',
      type: customMetadata.type,
      comparator: customMetadata.comparator || (customMetadata.type === 'numeric' ? 'higher_lower' : 'exact'),
      unitSymbol: customMetadata.unit_symbol || '',
      icon: customMetadata.icon || 'circle-dot',
      categories: customMetadata.categories || [],
      data: rows.map((r) => ({ country_id: r.country_id, value: r.value })),
      remoteId: cloudLink?.remoteId || null,
      ownerId: cloudLink?.ownerId || null,
      ownerUsername: cloudLink?.ownerUsername || null,
      visibility: cloudLink?.visibility || null,
      readOnly: !!cloudLink && !canPushCloudLink(cloudLink, auth.user?.id, auth.user?.role),
    }

    loadedDraft.baselineSnapshot = JSON.stringify({
      id: loadedDraft.id,
      label: loadedDraft.label,
      description: loadedDraft.description,
      type: loadedDraft.type,
      comparator: loadedDraft.comparator,
      unitSymbol: loadedDraft.unitSymbol,
      icon: loadedDraft.icon,
      categories: [...loadedDraft.categories],
      data: loadedDraft.data.map((row) => ({ country_id: row.country_id, value: row.value })),
    })

    newClueDraft = loadedDraft
    navigateTo(loadedDraft.readOnly ? 'view-clue' : 'edit-clue')
  }

  async function openPublishedCluePack(cluePackId: string) {
    try {
      const db = await getDB()
      await syncWorkspaceLinkedClues(db, getCloudDetailFetcher(auth))
      const detail = await getCluePack(cluePackId)
      const imported = await importPublishedClueToLocal(db, detail)
      await game.refreshCustomClueCatalog(false)
      await openLocalCustomClue(imported.localId)
    } catch (error) {
      toastStore.push('Could not open the published clue. Please try again.')
    }
  }

  async function removeViewedLocalCopy() {
    const deleteId = newClueDraft.id
    const db = await getDB()
    const existingClues = await loadWorkspaceCustomClues(db)
    await saveWorkspaceCustomClues(db, existingClues.filter((c) => c.id !== deleteId))

    const selected = game.userClues.filter((id: string) => id !== deleteId)
    const availableIds = existingClues.map((c) => c.id).filter((id) => id !== deleteId)
    let nextSelected = [...selected]
    for (const id of availableIds) {
      if (nextSelected.length >= 5) break
      if (!nextSelected.includes(id)) nextSelected.push(id)
    }
    for (const id of DEFAULT_CLUE_IDS) {
      if (nextSelected.length >= 5) break
      if (id !== deleteId && !nextSelected.includes(id)) nextSelected.push(id)
    }
    await saveWorkspaceSelectedClues(db, nextSelected.slice(0, 5))
    await removeWorkspaceCustomRowsForClue(db, deleteId)

    const links = await loadWorkspaceCloudLinks(db)
    delete links[deleteId]
    await saveWorkspaceCloudLinks(db, links)

    await game.refreshCustomClueCatalog(false)
    goBack()
  }

  async function cycleTheme() {
    currentThemeMode = await setThemeMode(cycleThemeMode(currentThemeMode));
  }

  function openClearCacheWarning() {
    navDirection = "forward";
    view = "clear-cache-warning";
  }

  function openAdminPanel() {
    navDirection = 'forward'
    view = 'admin-panel'
  }

  function openResetLeaderboardWarning() {
    navDirection = 'forward'
    view = 'admin-reset-leaderboard-warning'
  }

  async function confirmClearCache() {
    if (clearingCache) return;
    clearingCache = true;
    await clearAllCachedData();
    window.location.reload();
  }

  async function confirmResetLeaderboard() {
    if (resettingLeaderboard) return
    resettingLeaderboard = true
    try {
      await auth.request<void>(API_PATHS.leaderboards.ranked, { method: 'DELETE' })
      toastStore.push('Ranked leaderboard reset.', 'success')
      window.history.back()
    } catch (error) {
      toastStore.push('Failed to reset leaderboard. Please try again.')
    } finally {
      resettingLeaderboard = false
    }
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
              <button class="settings-item" onclick={openClues} disabled={mode === 'ranked'}>
                <div class="settings-item-icon">
                  <CircleDot />
                </div>
                <div class="settings-item-text">
                  <span>Clues</span>
                  <span class="muted">{mode === 'ranked' ? 'Unavailable in ranked mode' : 'Customize game clues'}</span>
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
              {#if auth.isAuthenticated && auth.user?.role === 'ADMIN'}
                <button class="settings-item" onclick={openAdminPanel}>
                  <div class="settings-item-icon">
                    <Shield />
                  </div>
                  <div class="settings-item-text">
                    <span>Admin Panel</span>
                    <span class="muted">Manage ranked rounds and admin actions</span>
                  </div>
                </button>
              {/if}
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

      {#if view === "admin-panel"}
        <AdminPanelSettings onBack={goBack} onClose={close} onNavigate={navigateTo} direction={navDirection} />
      {/if}

      {#if view === "admin-ranked-round"}
        <AdminRankedRoundSettings {auth} onBack={goBack} onClose={close} direction={navDirection} />
      {/if}

      {#if view === 'admin-reset-leaderboard-warning'}
        <div class="view-container warning-view" in:fly={{ x: 20, duration: 250, delay: 100 }} out:fly={{ x: 20, duration: 200 }}>
          <div class="modal-header">
            <h2>Reset leaderboard</h2>
            <button class="icon-btn" aria-label="Close" onclick={close} disabled={resettingLeaderboard}><X /></button>
          </div>
          <div class="warning-body">
            <div class="warning-icon">
              <Trophy size={20} />
            </div>
            <p class="warning-text">
              This will delete ranked sessions, clue usage, discovery progress, and leaderboard stats for all players.
            </p>
            <div class="warning-actions">
              <button class="warning-btn muted" onclick={goBack} disabled={resettingLeaderboard}>Keep leaderboard</button>
              <button class="warning-btn danger" onclick={confirmResetLeaderboard} disabled={resettingLeaderboard}>
                {resettingLeaderboard ? 'Resetting...' : 'Reset'}
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if view === "clues"}
        <CluesSettings
          {game}
          {auth}
          {mode}
          onBack={goBack}
          onClose={close}
          onNavigate={navigateTo}
          onEditCustomClue={openLocalCustomClue}
          onBeforeExplore={() => getDB().then(async (db) => { await syncWorkspaceLinkedClues(db, getCloudDetailFetcher(auth), { force: true }); await game.refreshCustomClueCatalog(false) })}
          direction={navDirection}
          bind:newClueDraft
        />
      {/if}

      {#if view === "explore-clues"}
        <ExploreCluesSettings
          onBack={goBack}
          onClose={close}
          direction={navDirection}
          onOpenCluePack={openPublishedCluePack}
        />
      {/if}

      {#if view === "add-clue"}
        <AddClueSettings
          {game}
          {auth}
          onBack={goBack}
          onClose={close}
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
          {auth}
          onBack={goBack}
          onClose={close}
          onNavigate={navigateTo}
          direction={navDirection}
          bind:hasUnsavedChanges={editHasUnsavedChanges}
          bind:discardPromptVisible={editDiscardPromptVisible}
          bind:newClueDraft
        />
      {/if}

      {#if view === "view-clue"}
        <ViewClueSettings
          {auth}
          onBack={goBack}
          onClose={close}
          onNavigate={navigateTo as (view: 'dataset-editor') => void}
          onRemoveLocalCopy={removeViewedLocalCopy}
          direction={navDirection}
          bind:newClueDraft
        />
      {/if}

      {#if view === "icon-picker"}
        <IconPickerSettings
          onBack={goBack}
          onClose={close}
          direction={navDirection}
          bind:newClueDraft
        />
      {/if}

      {#if view === "dataset-editor"}
        <DatasetEditorSettings
          {game}
          onBack={goBack}
          onClose={close}
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

  .settings-item:disabled {
    cursor: default;
    opacity: 0.5;
  }

  @media (hover: hover) {
    .settings-item:hover:not(:disabled) {
      background: var(--hover-soft);
    }
  }

  .settings-item:active:not(:disabled) {
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
