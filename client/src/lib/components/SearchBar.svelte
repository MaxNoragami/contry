<script lang="ts">
  import { Flag, X, SendHorizontal } from "lucide-svelte";
  import { fade } from "svelte/transition";
  import confetti from "canvas-confetti";
  import { APP_TIMINGS } from "../config/app";
  import SuggestionsDropdown from "./SuggestionsDropdown.svelte";

  interface Props {
    query: string;
    suggestions: string[];
    onQueryChange: (value: string) => void;
    onPreview: (country: string | null) => void;
    onValidate: (country: string) => boolean;
    onSubmit: (country: string) => Promise<{ valid: boolean; correct?: boolean }> | { valid: boolean; correct?: boolean };
    hasWon?: boolean;
    gameOver?: boolean;
    targetCountryName?: string | null;
    onReset?: () => void;
    onGiveUp?: () => void;
    placeholder?: string;
    giveUpDisabled?: boolean;
    resetLabel?: string;
    resetDisabled?: boolean;
    inputRef?: HTMLInputElement | undefined;
    isFocused?: boolean;
  }

  let {
    query,
    suggestions,
    onQueryChange,
    onPreview,
    onValidate,
    onSubmit,
    hasWon = false,
    gameOver = false,
    targetCountryName = null,
    onReset,
    onGiveUp,
    placeholder = "Type cōntry name",
    giveUpDisabled = false,
    resetLabel = "Play again!",
    resetDisabled = false,
    inputRef = $bindable(),
    isFocused = $bindable(false),
  }: Props = $props();

  let selectedIndex = $state(-1);
  let suggestionsEl: HTMLUListElement | undefined = $state();
  let shaking = $state(false);
  let submitting = $state(false);
  let virtualKeyboardHidden = $state(false);
  let confettiFired = $state(false);

  let showClear = $derived(!gameOver && (query.length > 0 || isFocused));

  $effect(() => {
    if (hasWon && !confettiFired) {
      confettiFired = true;
      const gruvboxColors = ['#cc241d', '#98971a', '#d79921', '#458588', '#b16286', '#689d6a', '#fe8019'];
      confetti({
        particleCount: 150,
        angle: 60,
        spread: 60,
        origin: { x: 0, y: 1 },
        colors: gruvboxColors,
        zIndex: 1000
      });
      confetti({
        particleCount: 150,
        angle: 120,
        spread: 60,
        origin: { x: 1, y: 1 },
        colors: gruvboxColors,
        zIndex: 1000
      });
    } else if (!hasWon && confettiFired) {
      confettiFired = false;
    }
  });

  // Scroll the selected suggestion into view
  $effect(() => {
    if (selectedIndex >= 0 && suggestionsEl) {
      const item = suggestionsEl.children[selectedIndex] as
        | HTMLElement
        | undefined;
      item?.scrollIntoView({ block: "nearest" });
    }
  });

  // Reset selection when suggestions change
  $effect(() => {
    if (suggestions.length === 0) {
      selectedIndex = -1;
    } else if (selectedIndex >= suggestions.length) {
      selectedIndex = suggestions.length - 1;
    } else if (selectedIndex === -1) {
      selectedIndex = 0;
    }
  });

  function handleInput(e: Event) {
    const value = (e.target as HTMLInputElement).value;
    onQueryChange(value);
    selectedIndex = 0;
  }

  function doSubmit(country: string) {
    if (submitting || !country.trim()) return;
    submitting = true;

    if (window.matchMedia('(orientation: landscape) and (max-height: 500px)').matches) {
      virtualKeyboardHidden = true;
    }

    if (!onValidate(country)) {
      // Invalid input — shake immediately, do not send
      shaking = true;
      setTimeout(() => {
        shaking = false;
        submitting = false;
        inputRef?.focus();
      }, APP_TIMINGS.guessShakeMs);
      return;
    }

    // Fill input with the valid country name first
    onQueryChange(country);
    selectedIndex = -1;

    // Brief pause so user sees the filled input, then submit
    setTimeout(async () => {
      const result = await onSubmit(country);

      if (!result.correct) {
        // Valid but wrong guess — shake, then clear it
        shaking = true;
        setTimeout(() => {
          shaking = false;
          onQueryChange("");
          submitting = false;
          inputRef?.focus();
        }, APP_TIMINGS.guessShakeMs);
      } else {
        // Correct — just clear
        onQueryChange("");
        submitting = false;
      }
    }, APP_TIMINGS.submitPreviewMs);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (submitting) return;

    if (suggestions.length > 0) {
      if (e.key === "ArrowDown" || (e.key === "Tab" && !e.shiftKey)) {
        e.preventDefault();
        selectedIndex = (selectedIndex + 1) % suggestions.length;
        onPreview(suggestions[selectedIndex]);
        return;
      }
      if (e.key === "ArrowUp" || (e.key === "Tab" && e.shiftKey)) {
        e.preventDefault();
        selectedIndex =
          selectedIndex <= 0 ? suggestions.length - 1 : selectedIndex - 1;
        onPreview(suggestions[selectedIndex]);
        return;
      }
    }

    if (e.key === "Enter") {
      e.preventDefault();
      let country = "";
      if (selectedIndex >= 0 && selectedIndex < suggestions.length) {
        country = suggestions[selectedIndex];
      } else if (suggestions.length === 1) {
        country = suggestions[0];
      } else {
        country = query.trim();
      }
      if (country) doSubmit(country);
      return;
    }

    if (e.key === "Escape") {
      e.preventDefault();
      onQueryChange("");
      selectedIndex = -1;
      inputRef?.blur();
      return;
    }
  }

  function handleClear() {
    onQueryChange("");
    selectedIndex = -1;
    inputRef?.blur();
  }

  function selectSuggestion(country: string) {
    doSubmit(country);
  }

  function handleSubmitClick() {
    const country = query.trim();
    if (country) doSubmit(country);
  }

  function handleInputClick() {
    if (virtualKeyboardHidden) {
      virtualKeyboardHidden = false;
      if (inputRef) {
        inputRef.blur();
        setTimeout(() => inputRef?.focus(), APP_TIMINGS.keyboardRefocusMs);
      }
    }
  }

  function handleResetClick() {
    if (onReset) onReset();
  }

  function handleGiveUpClick() {
    if (onGiveUp) onGiveUp();
  }
</script>

<section class="island search-bar-island" aria-label="Guess input">
  {#if suggestions.length > 0 && !gameOver}
    <SuggestionsDropdown
      {suggestions}
      {selectedIndex}
      onSelect={selectSuggestion}
      bind:listRef={suggestionsEl}
    />
  {/if}

  <div class="input-row" class:shaking>
    <button
      type="button"
      class="round-btn icon-switch-btn"
      class:faded={gameOver}
      disabled={gameOver || (!showClear && giveUpDisabled)}
      aria-label={showClear ? "Clear input" : "Give up"}
      onmousedown={(e) => {
        e.preventDefault();
        if (showClear) handleClear();
        else handleGiveUpClick();
      }}
    >
      {#if showClear}
        <div class="icon-wrapper" transition:fade={{ duration: 150 }}>
          <X />
        </div>
      {:else}
        <div class="icon-wrapper" transition:fade={{ duration: 150 }}>
          <Flag />
        </div>
      {/if}
    </button>

    {#if gameOver}
      <button class="play-again-btn" type="button" onclick={handleResetClick} disabled={resetDisabled}>
        {resetLabel}
      </button>
    {:else}
      <label for="guess-country" class="sr-only">Country name</label>
      <input
        id="guess-country"
        type="text"
        inputmode={virtualKeyboardHidden ? "none" : "text"}
        value={query}
        oninput={handleInput}
        onkeydown={handleKeydown}
        onfocus={() => {
          isFocused = true;
          virtualKeyboardHidden = false;
        }}
        onblur={() => isFocused = false}
        onclick={handleInputClick}
        autocomplete="off"
        placeholder={placeholder}
        bind:this={inputRef}
      />
    {/if}

    <button
      type="button"
      class="round-btn submit"
      class:submitting
      class:faded={gameOver}
      aria-label="Submit guess"
      onmousedown={(e) => e.preventDefault()}
      onclick={handleSubmitClick}
      disabled={submitting || gameOver}
    >
      <SendHorizontal />
    </button>
  </div>
</section>

<style>
  .search-bar-island {
    padding: 8px 10px;
    position: relative;
  }

  .input-row {
    display: grid;
    grid-template-columns: 40px minmax(0, 1fr) 40px;
    gap: 6px;
  }

  .round-btn {
    width: 40px;
    height: 40px;
    border-radius: 10px;
    border: 1px solid color-mix(in oklab, var(--border) 78%, var(--text));
    background: var(--panel-2);
    color: var(--text);
    display: grid;
    place-items: center;
    cursor: pointer;
    transition:
      background 0.15s,
      border-color 0.15s;
  }

  @media (hover: hover) {
    .round-btn:hover:not(:disabled) {
      background: var(--panel-3);
      border-color: color-mix(in oklab, var(--border) 58%, var(--text));
    }

    .round-btn.submit:hover:not(:disabled) {
      background: color-mix(in oklab, var(--accent) 35%, var(--panel-2));
    }
  }

  .round-btn:active:not(:disabled) {
    background: var(--panel-3);
    border-color: color-mix(in oklab, var(--border) 58%, var(--text));
  }

  .round-btn.submit:active:not(:disabled) {
    background: color-mix(in oklab, var(--accent) 35%, var(--panel-2));
  }

  .round-btn.submit {
    background: color-mix(in oklab, var(--accent) 20%, var(--panel-2));
    transition:
      background 0.15s,
      border-color 0.15s,
      transform 0.1s;
  }

  .round-btn.submit.submitting {
    background: color-mix(in oklab, var(--accent) 35%, var(--panel-2));
  }

  .round-btn.submit.submitting {
    transform: scale(0.92);
  }

  .icon-switch-btn {
    position: relative;
  }

  .icon-wrapper {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.round-btn svg) {
    width: 17px;
    height: 17px;
  }

  input {
    width: 100%;
    height: 40px;
    border-radius: 10px;
    border: 1px solid color-mix(in oklab, var(--border) 78%, var(--text));
    background: color-mix(in oklab, var(--panel) 52%, var(--panel-2));
    color: var(--text);
    text-align: center;
    font-size: clamp(14px, 1.8vw, 18px);
    padding: 0 12px;
    transition: border-color 0.15s;
  }

  input::placeholder {
    color: var(--muted);
    transition: opacity 0.15s ease;
  }

  input:focus::placeholder {
    opacity: 0;
  }

  input:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: 2px;
  }

  .play-again-btn {
    width: 100%;
    height: 40px;
    border-radius: 10px;
    border: 1px solid var(--accent);
    background: var(--accent);
    color: var(--chip-fg);
    text-align: center;
    font-size: clamp(14px, 1.8vw, 18px);
    font-weight: bold;
    cursor: pointer;
    transition: transform 0.15s, background 0.15s;
  }

  .play-again-btn:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    background: color-mix(in oklab, var(--panel) 52%, var(--panel-2));
    border-color: color-mix(in oklab, var(--border) 78%, var(--text));
    color: var(--muted);
  }

  @media (hover: hover) {
    .play-again-btn:hover:not(:disabled) {
      background: color-mix(in oklab, var(--accent) 82%, var(--chip-bg));
      border-color: color-mix(in oklab, var(--accent) 82%, var(--chip-bg));
    }
  }

  .play-again-btn:active:not(:disabled) {
    background: color-mix(in oklab, var(--accent) 82%, var(--chip-bg));
    border-color: color-mix(in oklab, var(--accent) 82%, var(--chip-bg));
    transform: scale(0.96);
  }

  .round-btn.faded {
    opacity: 0.3;
    pointer-events: none;
  }



  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    margin: -1px;
    padding: 0;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    border: 0;
  }

  @media (orientation: landscape) and (max-height: 500px) {
    .search-bar-island {
      padding: 6px 8px;
    }

    .round-btn {
      width: 34px;
      height: 34px;
    }

    input {
      height: 34px;
    }

    .input-row {
      grid-template-columns: 34px minmax(0, 1fr) 34px;
    }
  }

  /* Shake animation for wrong guesses */
  @keyframes shake {
    0%,
    100% {
      transform: translateX(0);
    }
    15% {
      transform: translateX(-8px);
    }
    30% {
      transform: translateX(7px);
    }
    45% {
      transform: translateX(-6px);
    }
    60% {
      transform: translateX(4px);
    }
    75% {
      transform: translateX(-2px);
    }
  }

  .shaking {
    animation: shake 0.5s ease-out;
  }

  .shaking input {
    border-color: var(--bad) !important;
    outline-color: var(--bad) !important;
  }
</style>
