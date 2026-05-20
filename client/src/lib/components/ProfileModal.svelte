<script lang="ts">
  import { ArrowLeft, ChartColumnBig, CircleDot, Globe, IdCard, LogOut, UserRoundPlus, X, Trophy, Trash2 } from 'lucide-svelte'
  import { fade, fly } from 'svelte/transition'
  import DiscoveryStatsPage from './stats/DiscoveryStatsPage.svelte'
  import DistributionStatsPage from './stats/DistributionStatsPage.svelte'
  import ClueUsageStatsPage from './stats/ClueUsageStatsPage.svelte'
  import LeaderboardPage from './stats/LeaderboardPage.svelte'
  import { API_PATHS, APP_TIMINGS } from '../config/app'
  import { getProblemFieldErrors, getProblemMessage, type createAuthStore } from '../stores/auth.svelte'
  import { toastStore } from '../stores/toasts.svelte'

  type ProfileView = 'main' | 'login' | 'register' | 'about' | 'clear-ranked-warning' | 'logout-warning' | 'discovery' | 'distributions' | 'clues' | 'leaderboard'

  interface Props {
    auth: ReturnType<typeof createAuthStore>
    visible: boolean
    onAuthSuccess?: () => void
  }

  let { auth, visible = $bindable(false), onAuthSuccess }: Props = $props()

  let view = $state<ProfileView>('login')
  let direction = $state<'forward' | 'back'>('forward')
  let historyDepth = $state(0)
  let sessionId = $state<string | null>(null)
  let loginCredential = $state('')
  let loginPassword = $state('')
  let registerUsername = $state('')
  let registerEmail = $state('')
  let registerPassword = $state('')
  let fieldErrors = $state<Record<string, string[]>>({})
  let busy = $state(false)
  let loginPasswordRef: HTMLInputElement | undefined = $state()
  let registerEmailRef: HTMLInputElement | undefined = $state()
  let registerPasswordRef: HTMLInputElement | undefined = $state()
  let clearingData = $state(false)

  function buildModalState(targetView: ProfileView) {
    return {
      modal: 'profile',
      sessionId,
      view: targetView,
    }
  }

  function defaultView(): ProfileView {
    return auth.isAuthenticated ? 'main' : 'login'
  }

  function resetModalState() {
    view = defaultView()
    historyDepth = 0
    sessionId = null
    fieldErrors = {}
    busy = false
    clearingData = false
  }

  $effect(() => {
    if (!visible) {
      return
    }

    if (!sessionId) {
      sessionId = crypto.randomUUID()
      view = defaultView()
    }

    const authView = defaultView()
    if (!auth.isAuthenticated && view !== 'login' && view !== 'register') {
      view = authView
    }
    if (auth.isAuthenticated && (view === 'login' || view === 'register')) {
      view = 'main'
    }

    const currentState = window.history.state
    if (
      currentState?.modal !== 'profile' ||
      currentState?.sessionId !== sessionId ||
      currentState?.view !== view
    ) {
      window.history.pushState(buildModalState(view), '')
      historyDepth += 1
    }
  })

  function onPopState(event: PopStateEvent) {
    if (!visible) return

    if (
      event.state?.modal === 'profile' &&
      event.state.sessionId === sessionId &&
      typeof event.state.view === 'string'
    ) {
      direction = 'back'
      view = event.state.view as ProfileView
      if (historyDepth > 0) historyDepth -= 1
    } else {
      visible = false
      setTimeout(resetModalState, APP_TIMINGS.modalResetMs)
    }
  }

  function close() {
    if (view === 'clear-ranked-warning' || view === 'logout-warning') {
      window.history.back()
      return
    }

    if (historyDepth > 0) {
      window.history.go(-historyDepth)
    }
    visible = false
    setTimeout(resetModalState, APP_TIMINGS.modalResetMs)
  }

  function goBack() {
    window.history.back()
  }

  function openView(nextView: ProfileView) {
    direction = 'forward'
    view = nextView
  }

  function getTransitionX(targetDirection: 'forward' | 'back', phase: 'in' | 'out') {
    const isForward = direction === targetDirection
    if (phase === 'in') {
      return isForward ? 20 : -20
    }

    return isForward ? -20 : 20
  }

  function handleBackdropClick(event: MouseEvent) {
    if (event.target === event.currentTarget) close()
  }

  function handleKeydown(event: KeyboardEvent) {
    if (!visible) return
    if (event.key === 'Escape') {
      event.preventDefault()
      if (view !== defaultView()) {
        window.history.back()
      } else {
        close()
      }
    }
  }

  async function handleLogin() {
    busy = true
    fieldErrors = {}

    try {
      await auth.login(loginCredential.trim(), loginPassword)
      loginPassword = ''
      view = 'main'
      onAuthSuccess?.()
    } catch (error) {
      fieldErrors = getProblemFieldErrors(error)
      toastStore.push(getProblemMessage(error))
    } finally {
      busy = false
    }
  }

  async function handleRegister() {
    busy = true
    fieldErrors = {}

    try {
      await auth.register(registerUsername.trim(), registerEmail.trim(), registerPassword)
      registerPassword = ''
      view = 'main'
      onAuthSuccess?.()
    } catch (error) {
      fieldErrors = getProblemFieldErrors(error)
      toastStore.push(getProblemMessage(error))
    } finally {
      busy = false
    }
  }

  function openClearDataWarning() {
    direction = 'forward'
    view = 'clear-ranked-warning'
  }

  function openLogoutWarning() {
    direction = 'forward'
    view = 'logout-warning'
  }

  async function confirmLogout() {
    busy = true
    fieldErrors = {}

    try {
      await auth.logout()
      if (historyDepth > 0) {
        window.history.go(-historyDepth)
      }
      visible = false
      setTimeout(resetModalState, APP_TIMINGS.modalResetMs)
    } catch (error) {
      toastStore.push(getProblemMessage(error))
    } finally {
      busy = false
    }
  }

  function clearFieldError(fieldName: string) {
    if (!(fieldName in fieldErrors)) return
    fieldErrors = Object.fromEntries(Object.entries(fieldErrors).filter(([key]) => key !== fieldName))
  }

  async function handleClearData() {
    clearingData = true
    try {
      await auth.request<void>(API_PATHS.ranked.statsMe, { method: 'DELETE' })
      toastStore.push('All ranked data cleared.')
      window.history.back()
    } catch (error) {
      toastStore.push('Failed to clear data. Please try again.')
    } finally {
      clearingData = false
    }
  }
</script>

<svelte:window onpopstate={onPopState} onkeydown={handleKeydown} />

{#if visible}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="modal-backdrop" onclick={handleBackdropClick} transition:fade={{ duration: 200 }}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="modal-content" onclick={(event) => event.stopPropagation()}>
      {#if auth.isAuthenticated && view === 'main'}
        <div class="view-container view-container--compact" in:fly={{ x: -20, duration: 250, delay: 100 }} out:fly={{ x: -20, duration: 200 }}>
          <div class="modal-header">
            <div></div>
            <h2>User profile</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}><X /></button>
          </div>

          <div class="modal-body">
            <div class="settings-list menu-actions">
              <button class="settings-item" onclick={() => openView('about')}>
                <div class="settings-item-icon"><IdCard /></div>
                <div class="settings-item-text">
                  <span>About me</span>
                  <span class="muted">See your account details</span>
                </div>
              </button>

              <button class="settings-item" onclick={() => openView('leaderboard')}>
                <div class="settings-item-icon"><Trophy /></div>
                <div class="settings-item-text">
                  <span>Leaderboard</span>
                  <span class="muted">See the global ranked leaderboard</span>
                </div>
              </button>

              <button class="settings-item" onclick={() => openView('discovery')}>
                <div class="settings-item-icon"><Globe /></div>
                <div class="settings-item-text">
                  <span>Cōntry discovery</span>
                  <span class="muted">See which countries you've solved in ranked</span>
                </div>
              </button>

              <button class="settings-item" onclick={() => openView('distributions')}>
                <div class="settings-item-icon"><ChartColumnBig /></div>
                <div class="settings-item-text">
                  <span>Distributions</span>
                  <span class="muted">Review your ranked guess patterns</span>
                </div>
              </button>

              <button class="settings-item" onclick={() => openView('clues')}>
                <div class="settings-item-icon"><CircleDot /></div>
                <div class="settings-item-text">
                  <span>Clues</span>
                  <span class="muted">See which clues show up most often in ranked</span>
                </div>
              </button>

              <button class="settings-item settings-item-danger" onclick={openLogoutWarning} disabled={busy}>
                <div class="settings-item-icon settings-item-icon-danger"><LogOut /></div>
                <div class="settings-item-text">
                  <span>Log out</span>
                  <span class="muted">End this browser session</span>
                </div>
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if !auth.isAuthenticated && view === 'login'}
        <div class="view-container view-container--auth" in:fly={{ x: -20, duration: 250, delay: 100 }} out:fly={{ x: -20, duration: 200 }}>
          <div class="modal-header">
            <h2>Account</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}><X /></button>
          </div>

          <div class="auth-panel auth-panel--compact">
            <div class="auth-intro">
              <UserRoundPlus size={18} />
              <div>
                <strong>Log in to play ranked</strong>
                <p>Your arcade progress stays local. Ranked requires an account.</p>
              </div>
            </div>

            <label class="field">
              <span>Username or email</span>
              <input
                bind:value={loginCredential}
                type="text"
                autocomplete="username"
                placeholder="john_doe or john@example.com"
                class:field-input-error={!!fieldErrors.credential}
                oninput={() => clearFieldError('credential')}
                onkeydown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    loginPasswordRef?.focus();
                  }
                }}
              />
              {#if fieldErrors.credential}
                <small class="field-error">{fieldErrors.credential[0]}</small>
              {/if}
            </label>

            <label class="field">
              <span>Password</span>
              <input
                bind:this={loginPasswordRef}
                bind:value={loginPassword}
                type="password"
                autocomplete="current-password"
                placeholder="Enter your password"
                class:field-input-error={!!fieldErrors.password}
                oninput={() => clearFieldError('password')}
                onkeydown={(e) => {
                  if (e.key === 'Enter' && !busy && loginCredential.trim() && loginPassword) {
                    e.preventDefault();
                    handleLogin();
                  }
                }}
              />
              {#if fieldErrors.password}
                <small class="field-error">{fieldErrors.password[0]}</small>
              {/if}
            </label>

            <button class="primary-btn" onclick={handleLogin} disabled={busy || !loginCredential.trim() || !loginPassword}>
              {busy ? 'Logging in...' : 'Log in'}
            </button>

            <p class="switch-copy">
              Try to
              <button class="inline-link" onclick={() => openView('register')} disabled={busy}>register</button>
              instead.
            </p>
          </div>
        </div>
      {/if}

      {#if !auth.isAuthenticated && view === 'register'}
        <div class="view-container view-container--auth" in:fly={{ x: 20, duration: 250, delay: 100 }} out:fly={{ x: 20, duration: 200 }}>
          <div class="modal-header">
            <button class="icon-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
            <h2>Register</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}><X /></button>
          </div>

          <div class="auth-panel auth-panel--compact">
            <label class="field">
              <span>Username</span>
              <input
                bind:value={registerUsername}
                type="text"
                autocomplete="username"
                placeholder="john_doe"
                class:field-input-error={!!fieldErrors.username}
                oninput={() => clearFieldError('username')}
                onkeydown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    registerEmailRef?.focus();
                  }
                }}
              />
              {#if fieldErrors.username}
                <small class="field-error">{fieldErrors.username[0]}</small>
              {/if}
            </label>

            <label class="field">
              <span>Email</span>
              <input
                bind:this={registerEmailRef}
                bind:value={registerEmail}
                type="email"
                autocomplete="email"
                placeholder="john@example.com"
                class:field-input-error={!!fieldErrors.email}
                oninput={() => clearFieldError('email')}
                onkeydown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    registerPasswordRef?.focus();
                  }
                }}
              />
              {#if fieldErrors.email}
                <small class="field-error">{fieldErrors.email[0]}</small>
              {/if}
            </label>

            <label class="field">
              <span>Password</span>
              <input
                bind:this={registerPasswordRef}
                bind:value={registerPassword}
                type="password"
                autocomplete="new-password"
                placeholder="Create a password"
                class:field-input-error={!!fieldErrors.password}
                oninput={() => clearFieldError('password')}
                onkeydown={(e) => {
                  if (e.key === 'Enter' && !busy && registerUsername.trim() && registerEmail.trim() && registerPassword) {
                    e.preventDefault();
                    handleRegister();
                  }
                }}
              />
              {#if fieldErrors.password}
                <small class="field-error">{fieldErrors.password[0]}</small>
              {/if}
            </label>

            <button class="primary-btn" onclick={handleRegister} disabled={busy || !registerUsername.trim() || !registerEmail.trim() || !registerPassword}>
              {busy ? 'Creating account...' : 'Create account'}
            </button>

            <p class="switch-copy">
              Already have an account?
              <button class="inline-link" onclick={goBack} disabled={busy}>Log in</button>
            </p>
          </div>
        </div>
      {/if}

      {#if auth.isAuthenticated && view === 'about'}
        <div class="view-container view-container--compact" in:fly={{ x: getTransitionX('forward', 'in'), duration: 250, delay: 100 }} out:fly={{ x: getTransitionX('forward', 'out'), duration: 200 }}>
          <div class="modal-header">
            <button class="icon-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
            <h2>About me</h2>
            <button class="icon-btn" aria-label="Close" onclick={close}><X /></button>
          </div>

          <div class="modal-body">
            <div class="about-card menu-actions">
              <div class="about-row">
                <span class="about-label">Username</span>
                <strong>{auth.user?.username}</strong>
              </div>
              <div class="about-row">
                <span class="about-label">Email</span>
                <strong>{auth.user?.email}</strong>
              </div>
              <div class="about-row">
                <span class="about-label">Role</span>
                <strong>{auth.user?.role}</strong>
              </div>
              <div class="about-row">
                <span class="about-label">User id</span>
                <strong class="mono">{auth.user?.id}</strong>
              </div>

              <button
                class="settings-item settings-item-danger"
                onclick={openClearDataWarning}
                disabled={clearingData}
              >
                <div class="settings-item-icon settings-item-icon-danger"><Trash2 /></div>
                <div class="settings-item-text">
                  <span>Clear ranked data</span>
                  <span class="muted">Delete all your ranked stats and sessions</span>
                </div>
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if auth.isAuthenticated && view === 'clear-ranked-warning'}
        <div class="view-container warning-view" in:fly={{ x: getTransitionX('forward', 'in'), duration: 250, delay: 100 }} out:fly={{ x: getTransitionX('forward', 'out'), duration: 200 }}>
          <div class="modal-header">
            <h2>Clear ranked data</h2>
            <button class="icon-btn" aria-label="Close" onclick={close} disabled={clearingData}><X /></button>
          </div>

          <div class="warning-body">
            <div class="warning-icon">
              <Trash2 size={20} />
            </div>
            <p class="warning-text">
              This will delete all of your ranked stats, ranked sessions, and clue/discovery history tied to your account.
            </p>
            <div class="warning-actions">
              <button class="warning-btn muted" onclick={goBack} disabled={clearingData}>Discard</button>
              <button class="warning-btn danger" onclick={handleClearData} disabled={clearingData}>
                {clearingData ? 'Clearing...' : 'Confirm'}
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if auth.isAuthenticated && view === 'logout-warning'}
        <div class="view-container warning-view" in:fly={{ x: getTransitionX('forward', 'in'), duration: 250, delay: 100 }} out:fly={{ x: getTransitionX('forward', 'out'), duration: 200 }}>
          <div class="modal-header">
            <h2>End browser session</h2>
            <button class="icon-btn" aria-label="Close" onclick={close} disabled={busy}><X /></button>
          </div>

          <div class="warning-body">
            <div class="warning-icon">
              <LogOut size={20} />
            </div>
            <p class="warning-text">
              This will log you out of the current browser session and return ranked mode to its signed-out state.
            </p>
            <div class="warning-actions">
              <button class="warning-btn muted" onclick={goBack} disabled={busy}>Stay logged in</button>
              <button class="warning-btn danger" onclick={confirmLogout} disabled={busy}>
                {busy ? 'Logging out...' : 'Log out'}
              </button>
            </div>
          </div>
        </div>
      {/if}

      {#if auth.isAuthenticated && view === 'leaderboard'}
        <LeaderboardPage goBack={goBack} onClose={close} {direction} />
      {/if}

      {#if auth.isAuthenticated && view === 'discovery'}
        <DiscoveryStatsPage {auth} goBack={goBack} onClose={close} {direction} />
      {/if}

      {#if auth.isAuthenticated && view === 'distributions'}
        <DistributionStatsPage {auth} goBack={goBack} onClose={close} {direction} />
      {/if}

      {#if auth.isAuthenticated && view === 'clues'}
        <ClueUsageStatsPage {auth} goBack={goBack} onClose={close} {direction} />
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
    .modal-backdrop { align-items: center; }
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

  .view-container--auth {
    justify-content: flex-start;
  }

  .view-container--compact {
    justify-content: flex-start;
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
  }

  .modal-header h2 {
    margin: 0;
    font-size: 20px;
    font-weight: 500;
    text-align: left;
    flex: 1 1 auto;
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
    .icon-btn:hover:not(:disabled) { background: var(--hover-strong); }
  }

  .icon-btn:active:not(:disabled) { background: var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }
  .icon-btn:disabled { cursor: default; pointer-events: none; box-shadow: none; }

  .modal-body {
    flex: 1;
    overflow-y: auto;
    padding: 16px 0;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar { display: none; }

  .auth-panel--compact {
    width: auto;
    margin: 0 16px;
    padding-top: 10px;
    padding-bottom: 12px;
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

  .settings-item:last-child { border-bottom: none; }

  @media (hover: hover) {
    .settings-item:hover:not(:disabled) { background: var(--hover-soft); }
    .settings-item-danger:hover:not(:disabled) { background: color-mix(in oklab, var(--bad) 10%, transparent); }
  }

  .settings-item:active:not(:disabled) { background: var(--hover-soft); }
  .settings-item-danger:active:not(:disabled) { background: color-mix(in oklab, var(--bad) 20%, transparent); }
  .settings-item:disabled { opacity: 0.7; cursor: default; }
  .settings-item-icon { color: var(--info); }
  .settings-item-icon-danger { color: var(--bad); }
  .settings-item-text { display: flex; flex-direction: column; gap: 4px; }
  .settings-item-text span { font-size: 16px; }
  .settings-item-text .muted { font-size: 13px; color: var(--muted); }

  .auth-panel {
    display: flex;
    flex-direction: column;
    gap: 14px;
    padding: 18px 16px 24px;
  }

  .auth-intro {
    display: flex;
    gap: 12px;
    align-items: flex-start;
    margin-bottom: 4px;
    padding: 16px;
    border: 1px solid var(--border);
    border-radius: 12px;
    background: var(--panel-2);
  }

  .auth-intro strong {
    display: block;
    font-size: 15px;
    margin-bottom: 4px;
  }

  .auth-intro p {
    margin: 0;
    color: var(--muted);
    font-size: 13px;
    line-height: 1.45;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .field span {
    font-size: 13px;
    color: var(--muted);
  }

  .field input {
    height: 40px;
    border-radius: 10px;
    border: 1px solid var(--border);
    background: var(--panel-2);
    color: var(--text);
    padding: 0 12px;
    font: inherit;
  }

  .field input:focus-visible {
    outline: none;
    border-color: var(--border-strong);
    box-shadow: inset 0 0 0 1px var(--border-strong);
  }

  .primary-btn {
    height: 40px;
    border-radius: 10px;
    border: 1px solid color-mix(in oklab, var(--accent) 40%, var(--border));
    background: color-mix(in oklab, var(--accent) 18%, var(--panel-2));
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }

  .primary-btn:disabled {
    opacity: 0.65;
    cursor: default;
  }

  .switch-copy {
    margin: 4px 0 0;
    text-align: center;
    color: var(--muted);
    font-size: 14px;
  }

  .inline-link {
    border: none;
    background: transparent;
    color: var(--warn);
    font: inherit;
    font-weight: 600;
    cursor: pointer;
    padding: 0 2px;
  }

  .inline-link:disabled {
    opacity: 0.7;
    cursor: default;
  }

  .field-error {
    color: color-mix(in oklab, var(--bad) 86%, white);
    font-size: 12px;
    line-height: 1.4;
  }

  .field-input-error {
    border-color: var(--bad) !important;
    background: rgba(204, 36, 29, 0.12) !important;
    box-shadow: inset 0 0 0 1px rgba(204, 36, 29, 0.45);
  }

  .about-card {
    display: flex;
    flex-direction: column;
  }

  .about-row {
    display: flex;
    flex-direction: column;
    gap: 4px;
    padding: 16px 24px;
    border-bottom: 1px solid var(--border);
    background: transparent;
  }

  .about-row:last-child { border-bottom: none; }
  .about-card:has(.settings-item) .about-row:nth-last-child(2) { border-bottom: 1px solid var(--border); }
  .about-label { color: var(--muted); font-size: 13px; }
  .about-row strong { font-size: 15px; font-weight: 600; }
  .mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; word-break: break-all; }

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
