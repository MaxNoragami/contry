<script lang="ts">
  import { ArrowLeft, CircleAlert } from "lucide-svelte";
  import { fly } from "svelte/transition";
  import type { DraftClueData, NavDirection } from "./types";

  interface Props {
    onBack: () => void;
    direction: NavDirection;
    newClueDraft: DraftClueData;
  }

  let { onBack, direction, newClueDraft = $bindable() }: Props = $props();

  let searchQuery = $state("");
  let tagsData = $state<Record<string, string[]>>({});
  let errorMsg = $state<string | null>(null);
  let loading = $state(true);

  $effect(() => {
    fetch("https://unpkg.com/lucide-static@latest/tags.json")
      .then((r) => {
        if (!r.ok) throw new Error("Failed to fetch");
        return r.json();
      })
      .then((data) => {
        tagsData = data;
        loading = false;
      })
      .catch((err) => {
        errorMsg =
          "Could not load icons from network. Please check your connection.";
        loading = false;
      });
  });

  const filteredIcons = $derived.by(() => {
    if (!tagsData || Object.keys(tagsData).length === 0) return [];
    const q = searchQuery.toLowerCase().trim();
    const all = Object.entries(tagsData);
    if (!q) {
      return all.slice(0, 50).map((e) => e[0]);
    }
    const matches = all.filter(([name, tags]) => {
      return name.includes(q) || tags.some((t) => t.includes(q));
    });
    return matches.slice(0, 50).map((e) => e[0]);
  });

  function selectIcon(name: string) {
    newClueDraft.icon = name;
    onBack();
  }
</script>

<div
  class="view-container"
  in:fly={{ x: direction === "back" ? -20 : 20, duration: 250, delay: 100 }}
  out:fly={{ x: direction === "back" ? 20 : -20, duration: 200 }}
>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}>
      <ArrowLeft />
    </button>
    <div class="search-container">
      <input
        type="text"
        bind:value={searchQuery}
        placeholder="Search icons..."
      />
    </div>
  </div>

  <div class="modal-body icon-grid">
    {#if loading}
      <div class="message">Loading icons...</div>
    {:else if errorMsg}
      <div class="message error">
        <CircleAlert size={32} />
        <p>{errorMsg}</p>
        <button class="fallback-btn" onclick={() => selectIcon("circle-dot")}>
          Use default (circle-dot)
        </button>
      </div>
    {:else if filteredIcons.length === 0}
      <div class="message">No icons found.</div>
    {:else}
      {#each filteredIcons as iconName}
        <button class="icon-row" onclick={() => selectIcon(iconName)}>
          <div
            class="custom-icon"
            style="mask-image: url('https://unpkg.com/lucide-static@latest/icons/{iconName}.svg'); -webkit-mask-image: url('https://unpkg.com/lucide-static@latest/icons/{iconName}.svg');"
          ></div>
          <span class="icon-name">{iconName}</span>
        </button>
      {/each}
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
    gap: 12px;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
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
    flex-shrink: 0;
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

  .search-container {
    flex: 1;
    position: relative;
    display: flex;
    align-items: center;
  }

  .search-container input {
    width: 100%;
    background: var(--field-bg);
    border: none;
    padding: 10px;
    border-radius: 8px;
    color: var(--text);
    font-size: 15px;
    outline: none;
  }

  .search-container input:focus {
    box-shadow: inset 0 0 0 1px var(--info);
  }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 12px 20px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }
  .modal-body::-webkit-scrollbar {
    display: none;
  }

  .message {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 16px;
    padding: 40px 20px;
    color: var(--muted);
    text-align: center;
  }

  .message.error {
    color: var(--bad);
  }

  .fallback-btn {
    padding: 10px 16px;
    background: var(--field-bg);
    color: var(--text);
    border: none;
    border-radius: 6px;
    cursor: pointer;
    margin-top: 12px;
  }

  .icon-row {
    display: flex;
    align-items: center;
    gap: 16px;
    width: 100%;
    padding: 16px;
    background: transparent;
    border: none;
    border-bottom: 1px solid var(--border);
    color: var(--text);
    cursor: pointer;
    text-align: left;
  }

  @media (hover: hover) {
    .icon-row:hover {
      background: var(--hover-soft);
    }
  }

  .custom-icon {
    width: 24px;
    height: 24px;
    background-color: currentColor;
    mask-size: contain;
    mask-repeat: no-repeat;
    mask-position: center;
    -webkit-mask-size: contain;
    -webkit-mask-repeat: no-repeat;
    -webkit-mask-position: center;
  }

  .icon-name {
    font-size: 15px;
  }
</style>
