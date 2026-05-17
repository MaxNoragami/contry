<script lang="ts">
  import {
    ArrowLeft,
    Binoculars,
    RotateCcw,
    Save,
    GripVertical,
    Plus,
    Pencil,
  } from "lucide-svelte";
  import { fly } from "svelte/transition";
  import { APP_LIMITS, DEFAULT_CLUE_IDS, getLucideIconUrl } from "../../config/app";
  import type { createAuthStore } from '../../stores/auth.svelte';
  import type { GameMode } from '../../stores/game-mode.svelte';

  import type { ViewType, DraftClueData, NavDirection } from "./types";

  interface Props {
    game: any;
    auth: ReturnType<typeof createAuthStore>;
    mode: GameMode;
    onBack: () => void;
    onNavigate: (view: ViewType) => void;
    onEditCustomClue: (clueId: string) => void | Promise<void>;
    onBeforeExplore?: () => Promise<void> | void;
    direction: NavDirection;
    newClueDraft: DraftClueData;
  }

  let {
    game,
    auth,
    mode,
    onBack,
    onNavigate,
    onEditCustomClue,
    onBeforeExplore,
    direction,
    newClueDraft = $bindable(),
  }: Props = $props();

  const selectionLocked = $derived(mode === 'ranked' && auth.user?.role !== 'ADMIN' ? true : mode === 'ranked');

  // Clues state
  type ClueState = { id: string; selected: boolean };
  let draftList = $state<ClueState[]>([]);

  $effect(() => {
    const activeIds = game.userClues;
    const selectedSet = new Set(activeIds);

    const unselectedIds = game.availableClues
      .filter((c: any) => !selectedSet.has(c.id))
      .map((c: any) => c.id);

    draftList = [
      ...activeIds.map((id: string) => ({ id, selected: true })),
      ...unselectedIds.map((id: string) => ({ id, selected: false })),
    ];
  });

  const selectedDraftCount = $derived(
    draftList.filter((c) => c.selected).length,
  );
  const isModified = $derived.by(() => {
    const activeIds = game.userClues;
    const draftSelected = draftList.filter((c) => c.selected).map((c) => c.id);
    if (activeIds.length !== draftSelected.length) return true;
    for (let i = 0; i < activeIds.length; i++) {
      if (activeIds[i] !== draftSelected[i]) return true;
    }
    return false;
  });

  function resetToDefault() {
    const activeIds = [...DEFAULT_CLUE_IDS];
    const selectedSet = new Set(activeIds);
    const unselectedIds = game.availableClues
      .filter((c: any) => !selectedSet.has(c.id))
      .map((c: any) => c.id);

    draftList = [
      ...activeIds.map((id: string) => ({ id, selected: true })),
      ...unselectedIds.map((id: string) => ({ id, selected: false })),
    ];
  }

  async function saveClues() {
    if (selectedDraftCount !== APP_LIMITS.activeClueCount) return;
    if (!isModified) return;
    const newSelected = draftList.filter((c) => c.selected).map((c) => c.id);
    await game.saveClues(newSelected);
  }

  const isSaveDisabled = $derived(!isModified || selectedDraftCount !== APP_LIMITS.activeClueCount);

  let isDragging = $state(false);
  let draggedIndex = $state<number | null>(null);

  function dragStart(e: DragEvent, index: number) {
    draggedIndex = index;
    isDragging = true;
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = "move";
      e.dataTransfer.setData("text/plain", index.toString());
    }
  }

  function dragOver(e: DragEvent, index: number) {
    e.preventDefault();
    if (draggedIndex === null || draggedIndex === index) return;

    const newList = [...draftList];
    const draggedItem = newList[draggedIndex];

    newList.splice(draggedIndex, 1);
    newList.splice(index, 0, draggedItem);

    draftList = newList;
    draggedIndex = index;
  }

  function dragEnd() {
    isDragging = false;
    draggedIndex = null;
  }

  function handleTouchStart(e: TouchEvent, index: number) {
    const target = e.target as HTMLElement;
    if (!target.closest(".drag-handle")) return;

    draggedIndex = index;
    isDragging = true;
  }

  function handleTouchMove(e: TouchEvent) {
    if (!isDragging || draggedIndex === null) return;

    const touch = e.touches[0];
    const target = document.elementFromPoint(touch.clientX, touch.clientY);
    if (!target) return;

    const clueItem = target.closest(".clue-item") as HTMLElement;
    if (!clueItem) return;

    const indexStr = clueItem.dataset.index;
    if (!indexStr) return;

    const targetIndex = parseInt(indexStr, 10);
    if (targetIndex === draggedIndex) return;

    const newList = [...draftList];
    const draggedItem = newList[draggedIndex];

    newList.splice(draggedIndex, 1);
    newList.splice(targetIndex, 0, draggedItem);

    draftList = newList;
    draggedIndex = targetIndex;
  }

  function handleTouchEnd() {
    if (isDragging) {
      isDragging = false;
      draggedIndex = null;
    }
  }

  function handleTouchCancel() {
    isDragging = false;
    draggedIndex = null;
  }

  function handleAddNewClue() {
    newClueDraft = {
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
    };
    onNavigate("add-clue");
  }

  async function openExploreClues() {
    await onBeforeExplore?.()
    onNavigate('explore-clues')
  }
</script>

<div
  class="view-container clues-view"
  in:fly={{ x: direction === "back" ? -20 : 20, duration: 250, delay: 100 }}
  out:fly={{ x: direction === "back" ? 20 : -20, duration: 200 }}
>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}>
      <ArrowLeft />
    </button>
    <h2 class="centered-title">
      Clues
      {#if !selectionLocked}
        <span class="counter" class:error={selectedDraftCount !== APP_LIMITS.activeClueCount}
          >({selectedDraftCount}/{APP_LIMITS.activeClueCount})</span
        >
      {/if}
    </h2>
    {#if !selectionLocked}
      <div class="header-actions">
        <button
          class="icon-btn"
          aria-label="Reset to default"
          onclick={resetToDefault}
        >
          <RotateCcw />
        </button>
        <button
          class="icon-btn save-btn"
          class:is-modified={!isSaveDisabled}
          class:is-error={selectedDraftCount !== APP_LIMITS.activeClueCount}
          aria-label="Save"
          onclick={saveClues}
          disabled={isSaveDisabled}
        >
          <Save />
        </button>
      </div>
    {:else}
      <div class="header-spacer"></div>
    {/if}
  </div>
  <div class="modal-body clues-body">
    <div class="menu-actions">
      <button class="clues-header-row action-btn" onclick={handleAddNewClue}>
        <div class="plus-icon-container">
          <Plus size={20} />
        </div>
        <div class="clues-header-text">
          <span>Add a new clue</span>
          <span class="muted" style="font-size: 13px;"
            >You are required to provide a dataset</span
          >
        </div>
      </button>
      <button class="clues-header-row action-btn" onclick={openExploreClues}>
        <div class="plus-icon-container">
          <Binoculars size={20} />
        </div>
        <div class="clues-header-text">
          <span>Explore clues</span>
          <span class="muted" style="font-size: 13px;">Browse published clue packs</span>
        </div>
      </button>
    </div>

    {#if !selectionLocked}
      <div class="clues-list" role="list">
        {#each draftList as item, index (item.id)}
          {@const clueDef = game.availableClues.find(
            (c: any) => c.id === item.id,
          )}
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <div
            class="clue-item"
            class:is-dragged={draggedIndex === index}
            draggable="true"
            data-index={index}
            ondragstart={(e) => dragStart(e, index)}
            ondragover={(e) => dragOver(e, index)}
            ondragend={dragEnd}
            ontouchstart={(e) => handleTouchStart(e, index)}
            ontouchmove={handleTouchMove}
            ontouchend={handleTouchEnd}
            ontouchcancel={handleTouchCancel}
          >
            <div class="drag-handle">
              <GripVertical size={18} />
            </div>
            <div class="clue-icon-wrapper">
              {#if clueDef?.icon}
                {@const IconComponent = clueDef.icon}
                <IconComponent size={20} />
              {:else if clueDef?.customIcon}
                <div class="custom-icon" style={`mask-image: url('${getLucideIconUrl(clueDef.customIcon)}'); -webkit-mask-image: url('${getLucideIconUrl(clueDef.customIcon)}');`}></div>
              {/if}
            </div>
            <span
              class="clue-label"
              class:is-custom={clueDef?.source === "custom"}
            >
              {clueDef?.label || item.id}
            </span>
            {#if clueDef?.source === "custom"}
              <button
                class="edit-btn"
                aria-label="Edit custom clue"
                onclick={() => onEditCustomClue(item.id)}
              >
                <Pencil size={16} />
              </button>
            {/if}
            <label class="checkbox-wrapper">
              <input type="checkbox" bind:checked={item.selected} />
              <span class="checkmark"></span>
            </label>
          </div>
        {/each}
      </div>
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
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .counter {
    font-size: 14px;
    color: var(--muted);
    font-weight: 400;
    position: relative;
    top: 2px;
  }

  .counter.error {
    color: var(--bad);
  }

  .header-actions {
    display: flex;
    gap: 8px;
  }

  .header-spacer {
    width: 40px;
    height: 40px;
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
    pointer-events: none;
    box-shadow: none;
  }

  .back-btn {
    margin-left: -8px;
  }

  .save-btn {
    color: var(--muted);
  }

  .save-btn.is-modified {
    color: var(--accent);
  }

  .save-btn.is-error {
    color: var(--bad);
    opacity: 0.5;
    cursor: not-allowed;
  }

  .save-btn:disabled:not(.is-error) {
    color: var(--border-strong);
    cursor: default;
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

  /* Clues specific styles */
  .clues-body {
    padding-top: 8px;
  }

  .clues-header-row {
    display: flex;
    align-items: center;
    gap: 24px;
    padding: 16px 24px;
  }

  .menu-actions {
    display: flex;
    flex-direction: column;
    background: var(--panel);
    border-radius: 12px;
    overflow: hidden;
    margin: 0 16px 16px;
    border: 1px solid var(--border);
  }

  .action-btn {
    width: 100%;
    background: transparent;
    border: none;
    color: var(--text);
    text-align: left;
    cursor: pointer;
    transition: background 0.2s;
  }

  @media (hover: hover) {
    .action-btn:hover {
      background: var(--hover-soft);
    }
  }

  .action-btn:active {
    background: var(--hover-soft);
  }

  .plus-icon-container {
    color: var(--text);
  }

  .clues-header-text {
    display: flex;
    flex-direction: column;
  }

  .clues-header-text span {
    font-size: 16px;
    font-weight: 500;
  }

  .clues-header-text .muted {
    color: var(--muted);
    font-weight: 400;
  }

  .clues-list {
    display: flex;
    flex-direction: column;
  }

  .clue-item {
    display: flex;
    align-items: center;
    padding: 16px 24px;
    background: var(--panel);
    transition: background 0.2s;
  }

  .clue-item.is-dragged {
    opacity: 0.5;
    background: var(--panel-soft);
  }

  .drag-handle {
    color: color-mix(in oklab, var(--muted) 72%, var(--border-strong));
    cursor: grab;
    padding-right: 24px;
    display: grid;
    place-items: center;
    touch-action: none;
  }

  .drag-handle:active {
    cursor: grabbing;
  }

  .clue-icon-wrapper {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    background: var(--chip-bg);
    color: var(--chip-fg);
    display: grid;
    place-items: center;
    margin-right: 20px;
    flex-shrink: 0;
  }

  .clue-label {
    flex: 1;
    font-size: 16px;
    font-weight: 400;
  }

  .clue-label.is-custom {
    font-style: italic;
  }

  .edit-btn {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    border: none;
    background: transparent;
    color: var(--muted);
    display: grid;
    place-items: center;
    cursor: pointer;
    margin-left: 4px;
    transition: color 0.15s;
  }

  @media (hover: hover) {
    .edit-btn:hover {
      color: var(--accent);
    }
  }

  .edit-btn:active {
    color: var(--accent);
    background: color-mix(in oklab, var(--accent) 12%, var(--panel));
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

  /* Custom Checkbox Android-like */
  .checkbox-wrapper {
    display: block;
    position: relative;
    cursor: pointer;
    width: 24px;
    height: 24px;
    margin-left: 16px;
  }

  .checkbox-wrapper input {
    position: absolute;
    opacity: 0;
    cursor: pointer;
    height: 0;
    width: 0;
  }

  .checkmark {
    position: absolute;
    top: 0;
    left: 0;
    height: 24px;
    width: 24px;
    background-color: transparent;
    border: 2px solid var(--border-strong);
    border-radius: 4px;
    transition: all 0.2s;
  }

  .checkbox-wrapper input:checked ~ .checkmark {
    background-color: var(--info);
    border-color: var(--info);
  }

  .checkmark:after {
    content: "";
    position: absolute;
    display: none;
  }

  .checkbox-wrapper input:checked ~ .checkmark:after {
    display: block;
  }

  .checkbox-wrapper .checkmark:after {
    left: 7px;
    top: 1px;
    width: 6px;
    height: 12px;
    border: solid var(--chip-fg);
    border-width: 0 2px 2px 0;
    transform: rotate(45deg);
  }
</style>
