<script lang="ts">
  import { ArrowLeft, Download, Table2, Trash2, X } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import type { createAuthStore } from '../../stores/auth.svelte'
  import type { DraftClueData, NavDirection } from './types'

  interface Props {
    auth: ReturnType<typeof createAuthStore>
    onBack: () => void
    onClose: () => void
    onNavigate: (view: 'dataset-editor') => void
    onRemoveLocalCopy: () => Promise<void> | void
    direction: NavDirection
    newClueDraft: DraftClueData
  }

  let { auth, onBack, onClose, onNavigate, onRemoveLocalCopy, direction, newClueDraft = $bindable() }: Props = $props()

  const ownerLabel = $derived.by(() => {
    if (!newClueDraft.ownerUsername) return 'Published clue'
    return newClueDraft.ownerUsername === auth.user?.username ? 'Your published clue' : `Published by ${newClueDraft.ownerUsername}`
  })

  const missingDataCount = $derived.by(() => newClueDraft.data.filter((d) => d.value === null || d.value === undefined || d.value === '').length)
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}><ArrowLeft /></button>
    <h2 class="centered-title">Clue Details</h2>
    <button class="icon-btn" aria-label="Close" onclick={onClose}><X /></button>
  </div>

  <div class="modal-body form-body">
    <div class="meta-card">
      <div class="meta-line"><span class="meta-label">Status</span><strong>{newClueDraft.readOnly ? 'Read-only import' : 'Local custom clue'}</strong></div>
      <div class="meta-line"><span class="meta-label">Owner</span><strong>{ownerLabel}</strong></div>
      <div class="meta-line"><span class="meta-label">Dataset ID</span><strong>{newClueDraft.id}</strong></div>
      <div class="meta-line"><span class="meta-label">Type</span><strong>{newClueDraft.type}</strong></div>
      <div class="meta-line"><span class="meta-label">Comparator</span><strong>{newClueDraft.comparator}</strong></div>
      {#if newClueDraft.unitSymbol}
        <div class="meta-line"><span class="meta-label">Unit</span><strong>{newClueDraft.unitSymbol}</strong></div>
      {/if}
    </div>

    <div class="form-group">
      <span class="fake-label">Label</span>
      <div class="readonly-value">{newClueDraft.label}</div>
    </div>

    <div class="form-group">
      <span class="fake-label">Description</span>
      <div class="readonly-value readonly-value--multiline">{newClueDraft.description}</div>
    </div>

    <div class="menu-actions">
      <button class="action-btn" onclick={() => onNavigate('dataset-editor')} disabled={newClueDraft.data.length === 0}>
        <div class="action-icon"><Table2 size={18} /></div>
        <div class="action-text">
          <span>View Data</span>
          <span class="muted">{newClueDraft.data.length} records{#if missingDataCount > 0} · {missingDataCount} empty{/if}</span>
        </div>
      </button>
      <button class="action-btn delete-action" onclick={onRemoveLocalCopy}>
        <div class="action-icon danger"><Trash2 size={18} /></div>
        <div class="action-text">
          <span>Remove local copy</span>
          <span class="muted">Keep the published clue, remove this browser copy</span>
        </div>
      </button>
    </div>

    <p class="note"><Download size={14} /> This clue can be used locally in arcade, but only its owner or an admin can edit the published version.</p>
  </div>
</div>

<style>
  .view-container { position:absolute; inset:0; display:flex; flex-direction:column; width:100%; height:100%; overflow-y:auto; scrollbar-width:none; -ms-overflow-style:none; }
  .view-container::-webkit-scrollbar { display:none; }
  .modal-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; background:var(--panel); position:sticky; top:0; z-index:2; }
  .centered-title { position:absolute; left:50%; transform:translateX(-50%); font-size:18px; font-weight:500; margin:0; }
  .header-spacer { width:40px; height:40px; }
  .icon-btn { width:40px; height:40px; border-radius:50%; border:none; background:transparent; color:var(--text); display:grid; place-items:center; cursor:pointer; transition:background .2s, box-shadow .2s, color .2s; outline:none; }
  @media (hover:hover) { .icon-btn:hover:not(:disabled) { background:var(--hover-strong); } }
  .icon-btn:active:not(:disabled) { background:var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }
  .form-body { padding:16px 20px 24px; display:flex; flex-direction:column; gap:18px; }
  .meta-card { display:flex; flex-direction:column; gap:10px; padding:14px 16px; border:1px solid var(--border); border-radius:12px; background:var(--panel-soft); }
  .meta-line { display:flex; justify-content:space-between; gap:16px; font-size:14px; }
  .meta-label { color:var(--muted); }
  .form-group { display:flex; flex-direction:column; gap:8px; }
  .fake-label { font-size:13px; color:var(--muted); font-weight:500; }
  .readonly-value { background:var(--field-bg); border:1px solid var(--field-border); padding:12px; border-radius:8px; color:var(--text); font-size:15px; }
  .readonly-value--multiline { line-height:1.45; white-space:pre-wrap; }
  .menu-actions { display:flex; flex-direction:column; background:var(--panel); border-radius:12px; overflow:hidden; border:1px solid var(--border); }
  .action-btn { display:flex; align-items:center; gap:16px; padding:16px; background:transparent; border:none; color:var(--text); text-align:left; cursor:pointer; border-bottom:1px solid var(--border); }
  .action-btn:last-child { border-bottom:none; }
  .action-btn:disabled { opacity:0.4; cursor:not-allowed; }
  @media (hover:hover) { .action-btn:hover:not(:disabled) { background:var(--hover-soft);} .delete-action:hover:not(:disabled) { background: color-mix(in oklab, var(--bad) 12%, var(--panel)); } }
  .action-btn:active:not(:disabled) { background:var(--hover-soft); }
  .action-icon { color:var(--muted); flex-shrink:0; }
  .action-icon.danger { color:var(--bad); }
  .action-text { display:flex; flex-direction:column; gap:2px; }
  .action-text span { font-size:15px; font-weight:500; }
  .action-text .muted { font-size:13px; color:var(--muted); font-weight:400; }
  .note { margin:0; display:flex; align-items:flex-start; gap:8px; color:var(--muted); font-size:13px; line-height:1.45; }
</style>
