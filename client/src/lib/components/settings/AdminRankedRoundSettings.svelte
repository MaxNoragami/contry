<script lang="ts">
  import { ArrowLeft, CalendarDays, ChevronDown, CircleDot, Dices, GripVertical, Save, Search, Trash2 } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import { API_PATHS } from '../../config/app'
  import type { AdminRankedChallengeEditorDto, AdminRankedClueOptionDto, RankedCountryOptionDto } from '../../api/client'
  import { getProblemMessage, type createAuthStore } from '../../stores/auth.svelte'
  import { toastStore } from '../../stores/toasts.svelte'
  import type { NavDirection } from './types'

  interface Props {
    auth: ReturnType<typeof createAuthStore>
    onBack: () => void
    direction: NavDirection
  }

  let { auth, onBack, direction }: Props = $props()

  let mode = $state<'current' | 'tomorrow' | 'picker'>('current')
  let pickedDate = $state(getDateString(new Date()))
  let loading = $state(true)
  let saving = $state(false)
  let deleting = $state(false)
  let error = $state<string | null>(null)
  let data = $state<AdminRankedChallengeEditorDto | null>(null)
  let baselineSnapshot = $state('')

  let targetCountryId = $state('')
  let selectedClueIds = $state<string[]>([])

  let countryQuery = $state('')
  let clueQuery = $state('')
  let countryPickerOpen = $state(false)
  let cluePickerOpen = $state(false)
  let deleteWarningOpen = $state(false)
  let isDragging = $state(false)
  let draggedIndex = $state<number | null>(null)
  const earliestSchedulableDate = getDateString(new Date(Date.now() + 1000 * 60 * 60 * 24 * 2))
  let hasLoadedOnce = false

  const activeDate = $derived.by(() => {
    const today = new Date()
    if (mode === 'current') return getDateString(today)
    if (mode === 'tomorrow') return getDateString(new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1))
    return pickedDate
  })

  const countries = $derived(data?.countries ?? [])
  const availableClues = $derived(data?.availableClues ?? [])
  const targetCountry = $derived.by(() => countries.find((country) => country.countryId === targetCountryId) || null)
  const selectedClues = $derived.by(
    () => selectedClueIds.map((id) => availableClues.find((clue) => clue.id === id)).filter(Boolean) as AdminRankedClueOptionDto[],
  )

  const filteredCountries = $derived.by(() => {
    const q = countryQuery.trim().toLowerCase()
    if (!q) return countries
    return countries.filter((country) => country.name.toLowerCase().includes(q) || country.countryId.toLowerCase().includes(q))
  })

  const filteredAvailableClues = $derived.by(() => {
    const q = clueQuery.trim().toLowerCase()
    const selected = new Set(selectedClueIds)
    const pool = availableClues.filter((clue) => !selected.has(clue.id))
    if (!q) return pool
    return pool.filter((clue) => clue.label.toLowerCase().includes(q) || clue.id.toLowerCase().includes(q) || (clue.ownerUsername || '').toLowerCase().includes(q))
  })

  const currentSnapshot = $derived(JSON.stringify({ date: activeDate, targetCountryId, clueIds: selectedClueIds }))
  const isDirty = $derived(currentSnapshot !== baselineSnapshot)
  const isSaveDisabled = $derived(!targetCountryId || selectedClueIds.length !== 5 || saving || deleting || !isDirty)
  const clueSummary = $derived.by(() => {
    if (selectedClues.length === 0) return 'Choose 5 clues'
    return selectedClues.map((clue) => clue.label).join(' • ')
  })

  $effect(() => {
    let cancelled = false
    loading = true
    error = null
    countryPickerOpen = false
    cluePickerOpen = false
    deleteWarningOpen = false

    void loadRound().then((result) => {
      if (cancelled || !result) return
      applyEditorState(result)
      hasLoadedOnce = true
      loading = false
    }).catch((err) => {
      if (cancelled) return
      if (!hasLoadedOnce) {
        error = getProblemMessage(err)
      } else {
        toastStore.push(getProblemMessage(err))
      }
      loading = false
    })

    return () => {
      cancelled = true
    }
  })

  async function loadRound() {
    return auth.request<AdminRankedChallengeEditorDto>(`${API_PATHS.ranked.challengeAdmin}/${activeDate}`)
  }

  function applyEditorState(result: AdminRankedChallengeEditorDto) {
    data = result
    targetCountryId = result.targetCountryId
    selectedClueIds = result.selectedClues.map((clue) => clue.id)
    countryQuery = ''
    clueQuery = ''
    baselineSnapshot = JSON.stringify({
      date: activeDate,
      targetCountryId: result.targetCountryId,
      clueIds: result.selectedClues.map((clue) => clue.id),
    })
  }

  function setMode(next: 'current' | 'tomorrow' | 'picker') {
    mode = next
  }

  function chooseCountry(country: RankedCountryOptionDto) {
    targetCountryId = country.countryId
    countryPickerOpen = false
  }

  function shuffleCountry() {
    if (countries.length === 0) return
    const random = countries[Math.floor(Math.random() * countries.length)]
    targetCountryId = random.countryId
  }

  function shuffleClues() {
    if (availableClues.length < 5) return
    const shuffled = [...availableClues].sort(() => Math.random() - 0.5).slice(0, 5)
    selectedClueIds = shuffled.map((clue) => clue.id)
  }

  function moveClue(from: number, to: number) {
    if (to < 0 || to >= selectedClueIds.length) return
    const next = [...selectedClueIds]
    const [item] = next.splice(from, 1)
    next.splice(to, 0, item)
    selectedClueIds = next
  }

  function addClue(id: string) {
    if (selectedClueIds.length >= 5 || selectedClueIds.includes(id)) return
    selectedClueIds = [...selectedClueIds, id]
  }

  function removeClue(id: string) {
    selectedClueIds = selectedClueIds.filter((clueId) => clueId !== id)
  }

  function dragStart(event: DragEvent, index: number) {
    draggedIndex = index
    isDragging = true
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move'
      event.dataTransfer.setData('text/plain', index.toString())
    }
  }

  function dragOver(event: DragEvent, index: number) {
    event.preventDefault()
    if (draggedIndex === null || draggedIndex === index) return

    const next = [...selectedClueIds]
    const [item] = next.splice(draggedIndex, 1)
    next.splice(index, 0, item)
    selectedClueIds = next
    draggedIndex = index
  }

  function dragEnd() {
    isDragging = false
    draggedIndex = null
  }

  function handleTouchStart(event: TouchEvent, index: number) {
    const target = event.target as HTMLElement
    if (!target.closest('.drag-handle')) return
    draggedIndex = index
    isDragging = true
  }

  function handleTouchMove(event: TouchEvent) {
    if (!isDragging || draggedIndex === null) return
    const touch = event.touches[0]
    const target = document.elementFromPoint(touch.clientX, touch.clientY)
    const row = target?.closest('.selected-row') as HTMLElement | null
    if (!row?.dataset.index) return
    const nextIndex = Number.parseInt(row.dataset.index, 10)
    if (Number.isNaN(nextIndex) || nextIndex === draggedIndex) return

    const next = [...selectedClueIds]
    const [item] = next.splice(draggedIndex, 1)
    next.splice(nextIndex, 0, item)
    selectedClueIds = next
    draggedIndex = nextIndex
  }

  function handleTouchEnd() {
    isDragging = false
    draggedIndex = null
  }

  const activityMessage = $derived.by(() => {
    if (loading) return 'Loading round settings...'
    if (saving) return mode === 'current' ? 'Saving and resetting today...' : 'Saving schedule...'
    if (deleting) return mode === 'current' ? 'Resetting ranked round...' : 'Deleting schedule...'
    return null
  })

  async function saveRound() {
    if (isSaveDisabled) return
    saving = true
    error = null
    try {
      const result = await auth.request<AdminRankedChallengeEditorDto>(`${API_PATHS.ranked.challengeAdmin}/${activeDate}`, {
        method: 'PUT',
        body: {
          targetCountryId,
          clueIds: selectedClueIds,
          resetSessions: mode === 'current',
        },
      })
      applyEditorState(result)
      toastStore.push(mode === 'current' ? 'Ranked round saved and reset.' : 'Ranked round schedule saved.', 'success')
    } catch (err) {
      error = getProblemMessage(err)
      toastStore.push(error)
    } finally {
      saving = false
    }
  }

  async function deleteSchedule() {
    deleting = true
    error = null
    try {
      await auth.request<{ challengeDateUtc: string; deleted: boolean; sessionsReset: boolean }>(`${API_PATHS.ranked.challengeAdmin}/${activeDate}`, { method: 'DELETE' })
      toastStore.push(mode === 'current' ? 'Ranked round reset.' : 'Scheduled round deleted.', 'success')
      const result = await loadRound()
      if (result) applyEditorState(result)
      deleteWarningOpen = false
    } catch (err) {
      error = getProblemMessage(err)
      toastStore.push(error)
    } finally {
      deleting = false
    }
  }

  function getDateString(date: Date): string {
    const year = date.getFullYear()
    const month = String(date.getMonth() + 1).padStart(2, '0')
    const day = String(date.getDate()).padStart(2, '0')
    return `${year}-${month}-${day}`
  }

</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  {#if deleteWarningOpen}
    <div class="warning-view" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
      <div class="modal-header">
        <h2 class="warning-title">Warning</h2>
      </div>
      <div class="warning-body">
        <div class="warning-icon"><Trash2 size={20} /></div>
        <p class="warning-text">{mode === 'current' ? 'Reset today\'s ranked round and clear that day\'s sessions/stats?' : 'Delete the saved ranked round schedule for this date?'}</p>
        <div class="warning-actions">
          <button class="warning-btn muted" onclick={() => (deleteWarningOpen = false)} disabled={deleting}>Keep Editing</button>
          <button class="warning-btn danger" onclick={deleteSchedule} disabled={deleting}>{deleting ? 'Working...' : mode === 'current' ? 'Reset Round' : 'Delete Schedule'}</button>
        </div>
      </div>
    </div>
  {:else}
    <div class="modal-header">
      <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}><ArrowLeft /></button>
      <h2 class="centered-title">Ranked Round</h2>
      <button class="icon-btn save-btn" class:is-ready={!isSaveDisabled} aria-label="Save round" onclick={saveRound} disabled={isSaveDisabled}><Save /></button>
    </div>

    <div class="modal-body form-body">
      <div class="mode-switch menu-actions menu-actions--inline">
        <button class:active={mode === 'current'} onclick={() => setMode('current')}>Current</button>
        <button class:active={mode === 'tomorrow'} onclick={() => setMode('tomorrow')}>Tomorrow</button>
        <button class:active={mode === 'picker'} onclick={() => setMode('picker')}>Date Picker</button>
      </div>

      {#if mode === 'picker'}
        <div class="form-group">
          <label for="ranked-date">UTC Date</label>
          <input id="ranked-date" type="date" bind:value={pickedDate} min={earliestSchedulableDate} />
        </div>
      {/if}

      {#if error}
        <div class="message">{error}</div>
      {:else if data}
        <div class="meta-card">
          <div class="meta-line"><span class="meta-label">Date</span><strong>{data.challengeDateUtc}</strong></div>
          <div class="meta-line"><span class="meta-label">State</span><strong>{data.isPersisted ? 'Scheduled' : 'Default preview'}</strong></div>
        </div>

        <div class="menu-actions section-stack">
          <button class="section-toggle" onclick={() => (countryPickerOpen = !countryPickerOpen)} aria-expanded={countryPickerOpen}>
            <div class="section-row__icon"><CalendarDays size={18} /></div>
            <div class="section-row__copy">
              <span>Country</span>
              <span class="muted">{targetCountry?.name || 'Choose a country'}</span>
            </div>
            <div class="section-actions">
              <div class="icon-btn mini-btn" role="button" tabindex="0" aria-label="Shuffle country" onclick={(event) => { event.stopPropagation(); shuffleCountry(); }} onkeydown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); shuffleCountry(); } }}><Dices size={16} /></div>
              <span style={`display:grid;place-items:center;transition:transform .15s;transform:rotate(${countryPickerOpen ? 180 : 0}deg);`}><ChevronDown size={16} /></span>
            </div>
          </button>

          {#if countryPickerOpen}
          <div class="section-dropdown">
            <label class="search-field"><Search size={16} /><input type="text" bind:value={countryQuery} placeholder="Search country..." /></label>
            <div class="picker-list">
              {#each filteredCountries as country (country.countryId)}
                <button class="picker-item" class:selected={country.countryId === targetCountryId} onclick={() => chooseCountry(country)}>
                  <span class="picker-label">{country.name}</span>
                  <span>{country.countryId}</span>
                </button>
              {/each}
            </div>
          </div>
          {/if}

          <button class="section-toggle" onclick={() => (cluePickerOpen = !cluePickerOpen)} aria-expanded={cluePickerOpen}>
            <div class="section-row__icon"><CircleDot size={18} /></div>
            <div class="section-row__copy">
              <span>Clues</span>
              <span class="muted">{clueSummary}</span>
            </div>
            <div class="section-actions">
              <div class="icon-btn mini-btn" role="button" tabindex="0" aria-label="Shuffle clues" onclick={(event) => { event.stopPropagation(); shuffleClues(); }} onkeydown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); shuffleClues(); } }}><Dices size={16} /></div>
              <span style={`display:grid;place-items:center;transition:transform .15s;transform:rotate(${cluePickerOpen ? 180 : 0}deg);`}><ChevronDown size={16} /></span>
            </div>
          </button>

          {#if cluePickerOpen}
          <div class="section-dropdown clue-manager">
            <div class="selected-clues" role="list">
              {#each selectedClues as clue, index (clue.id)}
                <div
                  class="selected-row"
                  class:is-dragged={draggedIndex === index}
                  role="listitem"
                  data-index={index}
                  draggable="true"
                  ondragstart={(event) => dragStart(event, index)}
                  ondragover={(event) => dragOver(event, index)}
                  ondragend={dragEnd}
                  ontouchstart={(event) => handleTouchStart(event, index)}
                  ontouchmove={handleTouchMove}
                  ontouchend={handleTouchEnd}
                  ontouchcancel={handleTouchEnd}
                >
                  <div class="drag-handle"><GripVertical size={18} /></div>
                  <div class="selected-copy">
                    <strong>{clue.label}</strong>
                    <span>{clue.source === 'published' && clue.ownerUsername ? `Published by ${clue.ownerUsername}` : 'Built-in clue'}</span>
                  </div>
                  <div class="selected-actions">
                    <button class="remove-btn" aria-label="Remove clue" onclick={() => removeClue(clue.id)}>×</button>
                  </div>
                </div>
              {/each}
            </div>

            <label class="search-field"><Search size={16} /><input type="text" bind:value={clueQuery} placeholder="Search clues..." /></label>
            <div class="picker-list">
              {#each filteredAvailableClues as clue (clue.id)}
                <button class="picker-item" onclick={() => addClue(clue.id)} disabled={selectedClueIds.length >= 5}>
                  <div class="picker-leading">
                    <span class="picker-label">{clue.label}</span>
                  </div>
                  <span>{clue.source === 'published' && clue.ownerUsername ? clue.ownerUsername : 'Built-in'}</span>
                </button>
              {/each}
            </div>
          </div>
          {/if}

          <button class="action-btn delete-action" onclick={() => (deleteWarningOpen = true)} disabled={deleting}>
            <div class="action-icon danger"><Trash2 size={18} /></div>
            <div class="action-text">
              <span>{mode === 'current' ? 'Reset Round' : 'Delete Scheduled Round'}</span>
              <span class="muted">{mode === 'current' ? 'Delete today\'s persisted challenge and reset that day\'s sessions' : 'Remove the saved schedule for this UTC date'}</span>
            </div>
          </button>
        </div>
      {/if}

      {#if activityMessage}
        <div class="activity-overlay" aria-live="polite">
          <div class="activity-panel">
            <div class="spinner"></div>
            <span>{activityMessage}</span>
          </div>
        </div>
      {/if}
    </div>
  {/if}
</div>

<style>
  .view-container { position:absolute; inset:0; display:flex; flex-direction:column; width:100%; height:100%; overflow-y:auto; scrollbar-width:none; -ms-overflow-style:none; }
  .view-container::-webkit-scrollbar { display:none; }
  .modal-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; background:var(--panel); position:sticky; top:0; z-index:2; }
  .centered-title { position:absolute; left:50%; transform:translateX(-50%); font-size:18px; font-weight:500; margin:0; }
  .icon-btn { width:40px; height:40px; border-radius:50%; border:none; background:transparent; color:var(--text); display:grid; place-items:center; cursor:pointer; }
  .icon-btn:disabled { opacity:0.45; cursor:default; }
  .save-btn { color:var(--border-strong); }
  .save-btn.is-ready { color:var(--accent); }
  .mini-btn { width:32px; height:32px; }
  .form-body { position:relative; padding:16px 20px 24px; display:flex; flex-direction:column; gap:16px; }
  .menu-actions { display:flex; flex-direction:column; background:var(--panel); border-radius:12px; overflow:hidden; border:1px solid var(--border); }
  .section-stack { overflow:visible; gap:0; }
  .menu-actions--inline { display:grid; grid-template-columns:1fr 1fr 1fr; }
  .menu-actions--inline button { min-height:42px; border:none; background:transparent; color:var(--muted); border-right:1px solid var(--border); }
  .menu-actions--inline button:last-child { border-right:none; }
  .menu-actions--inline button.active { background:var(--hover-soft); color:var(--text); }
  .section-toggle { width:100%; display:flex; align-items:center; gap:16px; padding:16px; border:none; background:transparent; color:var(--text); text-align:left; cursor:pointer; }
  .section-toggle + .section-toggle,
  .section-dropdown + .section-toggle,
  .section-dropdown + .action-btn,
  .section-toggle + .action-btn { border-top:1px solid var(--border); }
  .section-row__icon { color:var(--info); flex-shrink:0; }
  .section-row__copy { display:flex; flex-direction:column; gap:2px; min-width:0; }
  .section-row__copy span { font-size:15px; font-weight:500; }
  .section-row__copy .muted { font-size:13px; color:var(--muted); font-weight:400; }
  .section-actions { margin-left:auto; display:flex; align-items:center; gap:8px; color:var(--muted); flex-shrink:0; }
  .meta-card { border:1px solid var(--border); border-radius:12px; background:var(--panel-soft); padding:14px; display:flex; flex-direction:column; gap:12px; }
  .section-dropdown { display:flex; flex-direction:column; gap:12px; padding:16px 16px 16px; border-top:1px solid var(--border); background:var(--panel); }
  .meta-line { display:flex; justify-content:space-between; gap:12px; }
  .meta-label { color:var(--muted); }
  .form-group { display:flex; flex-direction:column; gap:8px; }
  label { font-size:13px; color:var(--muted); font-weight:500; }
  input[type="date"] { background:var(--field-bg); border:1px solid var(--field-border); padding:12px; border-radius:8px; color:var(--text); font-size:15px; outline:none; width:100%; }
  .search-field { display:grid; grid-template-columns:16px 1fr; gap:10px; align-items:center; min-height:42px; padding:0 12px; border:1px solid var(--border); border-radius:10px; background:var(--panel); color:var(--muted); }
  .search-field input { width:100%; border:none; background:transparent; color:var(--text); outline:none; font:inherit; }
  .picker-list { display:flex; flex-direction:column; max-height:260px; overflow:auto; border:1px solid var(--border); border-radius:10px; background:var(--panel-soft); }
  .picker-item { display:flex; justify-content:space-between; gap:12px; padding:12px 14px; border:none; border-bottom:1px solid var(--border); background:transparent; color:var(--text); cursor:pointer; text-align:left; }
  .picker-item:last-child { border-bottom:none; }
  .picker-item span { color:var(--muted); font-size:12px; }
  .picker-leading { display:flex; align-items:center; gap:12px; min-width:0; }
  .picker-item .picker-label { color:var(--text); font-size:15px; }
  .picker-item.selected { background:var(--hover-soft); }
  .selected-clues { display:flex; flex-direction:column; gap:0; }
  .selected-row { display:grid; grid-template-columns:auto 1fr auto; gap:12px; align-items:center; padding:12px 0; background:transparent; }
  .selected-row.is-dragged { opacity:0.5; background:var(--panel-soft); }
  .drag-handle { color:color-mix(in oklab, var(--muted) 72%, var(--border-strong)); cursor:grab; display:grid; place-items:center; flex-shrink:0; touch-action:none; }
  .drag-handle:active { cursor:grabbing; }
  .selected-copy { display:flex; flex-direction:column; gap:2px; min-width:0; }
  .selected-copy strong { font-size:14px; }
  .selected-copy span { color:var(--muted); font-size:12px; }
  .selected-actions { display:flex; gap:6px; flex-shrink:0; justify-self:end; }
  .remove-btn { width:28px; height:28px; border:none; background:transparent; color:var(--bad); cursor:pointer; font-size:22px; line-height:1; display:grid; place-items:center; }
  .action-btn { width:100%; display:flex; align-items:center; gap:16px; padding:16px; background:transparent; border:none; color:var(--text); text-align:left; cursor:pointer; }
  .action-btn:disabled { opacity:0.45; cursor:default; }
  .action-icon { color:var(--info); flex-shrink:0; }
  .action-icon.danger { color:var(--bad); }
  .action-text { display:flex; flex-direction:column; gap:2px; min-width:0; }
  .action-text span { font-size:15px; font-weight:500; }
  .action-text .muted { font-size:13px; color:var(--muted); font-weight:400; }
  .message { margin:auto 0; text-align:center; color:var(--muted); padding:24px 0; }
  .activity-overlay { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; background:color-mix(in oklab, var(--panel) 88%, transparent); backdrop-filter:blur(2px); z-index:3; }
  .activity-panel { display:flex; align-items:center; gap:12px; min-height:44px; padding:12px 16px; border-radius:12px; border:1px solid var(--border); background:var(--panel); color:var(--text); box-shadow:var(--shadow-lift); }
  .spinner { width:16px; height:16px; border:2px solid var(--border); border-top-color:var(--accent); border-radius:50%; animation:spin 1s linear infinite; }
  .warning-view { position:absolute; inset:0; display:flex; flex-direction:column; background:var(--panel); }
  .warning-title { margin:0 auto; font-size:18px; font-weight:500; }
  .warning-body { flex:1; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:20px; padding:24px; text-align:center; }
  .warning-icon { width:52px; height:52px; border-radius:999px; display:grid; place-items:center; color:var(--bad); background: color-mix(in oklab, var(--bad) 12%, var(--panel)); border:1px solid color-mix(in oklab, var(--bad) 36%, var(--border)); }
  .warning-text { margin:0; max-width:320px; font-size:16px; line-height:1.45; color:var(--text); }
  .warning-actions { display:flex; gap:12px; width:100%; max-width:320px; }
  .warning-btn { flex:1; border:none; border-radius:12px; padding:12px 16px; font-size:15px; font-weight:600; cursor:pointer; }
  .warning-btn:disabled { opacity:0.7; cursor:default; }
  .warning-btn.muted { background:var(--border); color:var(--text); }
  .warning-btn.danger { background: color-mix(in oklab, var(--bad) 30%, var(--panel)); color:var(--chip-bg); }
  @media (hover:hover) {
    .section-toggle:hover,
    .menu-actions--inline button:hover,
    .picker-item:hover:not(:disabled),
    .remove-btn:hover,
    .icon-btn:hover:not(:disabled),
    .action-btn:hover:not(:disabled) { background:var(--hover-soft); }
    .delete-action:hover:not(:disabled) { background: color-mix(in oklab, var(--bad) 12%, var(--panel)); }
  }
  @keyframes spin {
    from { transform:rotate(0deg); }
    to { transform:rotate(360deg); }
  }
</style>
