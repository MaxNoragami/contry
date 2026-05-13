<script lang="ts">
  import Header from "./lib/components/Header.svelte";
  import ClueBar from "./lib/components/ClueBar.svelte";
  import GuessList from "./lib/components/GuessList.svelte";
  import SearchBar from "./lib/components/SearchBar.svelte";
  import WorldMap from "./lib/components/WorldMap.svelte";
  import MapModal from "./lib/components/MapModal.svelte";
  import SettingsModal from "./lib/components/SettingsModal.svelte";
  import GiveUpModal from "./lib/components/GiveUpModal.svelte";
  import HelpModal from "./lib/components/HelpModal.svelte";
  import ProfileModal from "./lib/components/ProfileModal.svelte";
  import ToastStack from "./lib/components/ToastStack.svelte";
  import { createArcadeGameState } from "./lib/stores/game.svelte";
  import { initTheme } from "./lib/stores/theme";
  import { createAuthStore } from "./lib/stores/auth.svelte";
  import { createGameModeStore } from "./lib/stores/game-mode.svelte";
  import { createRankedGameState } from "./lib/stores/ranked-game.svelte";

  const auth = createAuthStore();
  const mode = createGameModeStore();
  const arcadeGame = createArcadeGameState();
  const rankedGame = createRankedGameState(auth);

  const game = $derived.by(() => (mode.current === "ranked" ? rankedGame : arcadeGame));

  let inputRef: HTMLInputElement | undefined = $state();
  let searchFocused = $state(false);
  let clueBarScrollEl: HTMLElement | undefined = $state();
  let guessListScrollEl: HTMLElement | undefined = $state();
  let settingsOpen = $state(false);
  let giveUpOpen = $state(false);
  let helpOpen = $state(false);
  let profileOpen = $state(false);
  let pendingRankedPrompt = $state(false);
  let previousAuthState = $state(false);
  let countdownString = $state("");

  $effect(() => {
    void initTheme();
    void auth.init();
    void arcadeGame.initGame();
  });

  $effect(() => {
    if (mode.current === 'ranked' && auth.isAuthenticated) {
      void rankedGame.initGame();
    }
  });

  $effect(() => {
    if (mode.current === 'ranked' && game.gameOver) {
      const updateCountdown = () => {
        const now = new Date();
        const tomorrow = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1));
        const diff = tomorrow.getTime() - now.getTime();

        const h = Math.floor(diff / (1000 * 60 * 60)).toString().padStart(2, '0');
        const m = Math.floor((diff / (1000 * 60)) % 60).toString().padStart(2, '0');
        const s = Math.floor((diff / 1000) % 60).toString().padStart(2, '0');

        countdownString = `Next in ${h}:${m}:${s}`;
      };

      updateCountdown();
      const interval = setInterval(updateCountdown, 1000);
      return () => clearInterval(interval);
    }
  });

  $effect(() => {
    if (previousAuthState && !auth.isAuthenticated && auth.lastLogoutReason === 'expired') {
      profileOpen = true;
      pendingRankedPrompt = true;
    }

    previousAuthState = auth.isAuthenticated;
  });

  $effect(() => {
    if (!auth.isAuthenticated) {
      rankedGame.clearSession();
      if (mode.current === 'ranked') {
        mode.setMode('arcade');
      }
    }
  });

  // ── Synchronized horizontal scroll ──────────────────
  $effect(() => {
    if (!clueBarScrollEl || !guessListScrollEl) return;

    let syncing = false;

    const sync = (source: HTMLElement, target: HTMLElement) => {
      source.addEventListener(
        "scroll",
        () => {
          if (syncing) return;
          syncing = true;
          target.scrollLeft = source.scrollLeft;
          requestAnimationFrame(() => (syncing = false));
        },
        { passive: true },
      );
    };

    sync(clueBarScrollEl, guessListScrollEl);
    sync(guessListScrollEl, clueBarScrollEl);
  });

  // ── Portrait media query ────────────────────────────
  let isPortrait = $state(false);

  $effect(() => {
    // max-width alone catches zoomed-in landscape that looks portrait-like
    const mq = window.matchMedia("(orientation: portrait), (max-width: 767px)");
    isPortrait = mq.matches;
    const handler = (e: MediaQueryListEvent) => (isPortrait = e.matches);
    mq.addEventListener("change", handler);
    return () => mq.removeEventListener("change", handler);
  });

  // ── Map fly-to ──────────────────────────────────────
  const flyTo = $derived.by(() => {
    if (!game.bestMatch) return null;
    return { lat: game.bestMatch.lat, lon: game.bestMatch.lon, zoom: 4 };
  });

  const markerPos = $derived.by(() => {
    if (!game.bestMatch) return null;
    return { lat: game.bestMatch.lat, lon: game.bestMatch.lon };
  });

  const guessedCountries = $derived(game.rows.map((r) => r.country));

  // ── Global keyboard navigation ─────────────────────
  function handleGlobalKeydown(e: KeyboardEvent) {
    const tag = (e.target as HTMLElement)?.tagName;
    const isInInput = tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";

    if (e.key === "/" && !isInInput) {
      e.preventDefault();
      inputRef?.focus();
      return;
    }

    if (e.key === "Escape" && isInInput) {
      return;
    }

    if (
      !isInInput &&
      e.key.length === 1 &&
      !e.ctrlKey &&
      !e.metaKey &&
      !e.altKey
    ) {
      inputRef?.focus();
    }
  }

  function handleQueryChange(value: string) {
    game.query = value;
  }

  async function handleSubmit(country: string): Promise<{
    valid: boolean;
    correct?: boolean;
  }> {
    return game.submitGuess(country);
  }

  function handleProfileClick() {
    pendingRankedPrompt = false;
    profileOpen = true;
  }

  function handleModeToggle() {
    if (mode.current === 'ranked') {
      mode.setMode('arcade');
      return;
    }

    if (!auth.isAuthenticated) {
      pendingRankedPrompt = true;
      profileOpen = true;
      return;
    }

    mode.setMode('ranked');
  }

  function handleAuthSuccess() {
    if (pendingRankedPrompt) {
      pendingRankedPrompt = false;
      mode.setMode('ranked');
      profileOpen = false;
      return;
    }

    profileOpen = true;
  }
</script>

<svelte:window onkeydown={handleGlobalKeydown} />

<ToastStack />

{#if game.loading}
  <div class="loading-overlay">
    <div class="spinner"></div>
    <p>Loading datasets...</p>
  </div>
{:else}
  <div class="app-shell">
    <div class="area-header">
      <Header
        onHelpClick={() => (helpOpen = true)}
        onProfileClick={handleProfileClick}
        onModeToggle={handleModeToggle}
        onSettingsClick={() => {
          if (mode.current === 'arcade') {
            settingsOpen = true;
          }
        }}
        mode={mode.current}
        isAuthenticated={auth.isAuthenticated}
        settingsDisabled={mode.current === 'ranked'}
      />
    </div>

    <div class="area-clues">
      <ClueBar
        clues={game.activeClues}
        bind:scrollContainer={clueBarScrollEl}
      />
    </div>

    <div class="area-guesses">
      {#if isPortrait && (game.isTyping || searchFocused || game.gameOver)}
        <MapModal
          visible={true}
          {flyTo}
          markerPosition={markerPos}
          highlightCountry={game.bestMatch?.name ?? null}
          {guessedCountries}
          errorCountry={game.errorCountry}
          correctCountry={game.correctCountry}
          gaveUpCountry={game.gaveUpCountry}
        />
      {/if}
      <GuessList
        rows={game.rows}
        isTyping={game.isTyping}
        bind:scrollContainer={guessListScrollEl}
      />
    </div>

    <div class="area-map">
      <div class="island map-island">
        <WorldMap
          {flyTo}
          markerPosition={markerPos}
          highlightCountry={game.bestMatch?.name ?? null}
          {guessedCountries}
          errorCountry={game.errorCountry}
          correctCountry={game.correctCountry}
          gaveUpCountry={game.gaveUpCountry}
        />
      </div>
    </div>

    <div class="area-search">
      <SearchBar
        query={game.query}
        suggestions={game.suggestions}
        onQueryChange={handleQueryChange}
        onPreview={(country) => {
          game.preview = country;
        }}
        onValidate={(country) => game.isValid(country)}
        onSubmit={handleSubmit}
        hasWon={game.hasWon}
        gameOver={game.gameOver}
        targetCountryName={game.targetCountryName}
        onReset={() => game.resetGame()}
        onGiveUp={() => (giveUpOpen = true)}
        placeholder={mode.current === 'ranked' ? 'Type cōntry name (ranked)' : 'Type cōntry name (arcade)'}
        giveUpDisabled={false}
        resetLabel={mode.current === 'ranked' ? countdownString : 'Play again!'}
        resetDisabled={mode.current === 'ranked'}
        bind:inputRef
        bind:isFocused={searchFocused}
      />
    </div>
  </div>

  <SettingsModal {game} bind:visible={settingsOpen} />

  <HelpModal
    {game}
    bind:visible={helpOpen}
    onClose={() => (helpOpen = false)}
  />

  <ProfileModal {auth} bind:visible={profileOpen} onAuthSuccess={handleAuthSuccess} />

  <GiveUpModal
    bind:visible={giveUpOpen}
    onConfirm={() => {
      giveUpOpen = false;
      void game.giveUp();
    }}
    onCancel={() => (giveUpOpen = false)}
  />
{/if}

<style>
  .loading-overlay {
    position: fixed;
    inset: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background: var(--panel);
    color: var(--text);
    z-index: 9999;
  }
  .spinner {
    width: 40px;
    height: 40px;
    border: 4px solid var(--border);
    border-top-color: var(--accent);
    border-radius: 50%;
    animation: spin 1s linear infinite;
    margin-bottom: 16px;
  }
  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
</style>
