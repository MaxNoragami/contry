<script lang="ts">
  import { ArrowLeft, CalendarDays, Shield, Trophy } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import type { NavDirection, ViewType } from './types'

  interface Props {
    onBack: () => void
    onNavigate: (view: ViewType) => void
    direction: NavDirection
  }

  let { onBack, onNavigate, direction }: Props = $props()
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={onBack}><ArrowLeft /></button>
    <h2 class="centered-title">Admin Panel</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    <div class="menu-actions">
      <button class="action-btn" onclick={() => onNavigate('admin-ranked-round')}>
        <div class="action-icon"><CalendarDays size={18} /></div>
        <div class="action-text">
          <span>Ranked Round</span>
          <span class="muted">Schedule target countries and ranked clues</span>
        </div>
      </button>
      <button class="action-btn delete-action" onclick={() => onNavigate('admin-reset-leaderboard-warning')}>
        <div class="action-icon danger"><Trophy size={18} /></div>
        <div class="action-text">
          <span>Reset Leaderboard</span>
          <span class="muted">Delete ranked sessions and rebuild the competitive state</span>
        </div>
      </button>
    </div>

    <p class="note"><Shield size={14} /> Admin actions affect live ranked gameplay and should be used intentionally.</p>
  </div>
</div>

<style>
  .view-container { position:absolute; inset:0; display:flex; flex-direction:column; width:100%; height:100%; }
  .modal-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; background:var(--panel); position:relative; z-index:2; }
  .centered-title { position:absolute; left:50%; transform:translateX(-50%); font-size:18px; font-weight:500; margin:0; }
  .header-spacer { width:40px; height:40px; }
  .icon-btn { width:40px; height:40px; border-radius:50%; border:none; background:transparent; color:var(--text); display:grid; place-items:center; cursor:pointer; }
  .modal-body { flex:1; overflow-y:auto; padding:16px; display:flex; flex-direction:column; gap:16px; }
  .menu-actions { display:flex; flex-direction:column; background:var(--panel); border-radius:12px; overflow:hidden; border:1px solid var(--border); }
  .action-btn { display:flex; align-items:center; gap:16px; padding:16px; background:transparent; border:none; color:var(--text); text-align:left; cursor:pointer; border-bottom:1px solid var(--border); }
  .action-btn:last-child { border-bottom:none; }
  @media (hover:hover) { .action-btn:hover { background:var(--hover-soft); } .delete-action:hover { background: color-mix(in oklab, var(--bad) 12%, var(--panel)); } }
  .action-btn:active { background:var(--hover-soft); }
  .action-icon { color:var(--info); flex-shrink:0; }
  .action-icon.danger { color:var(--bad); }
  .action-text { display:flex; flex-direction:column; gap:2px; }
  .action-text span { font-size:15px; font-weight:500; }
  .action-text .muted { font-size:13px; color:var(--muted); font-weight:400; }
  .note { margin:0; display:flex; gap:8px; color:var(--muted); font-size:13px; line-height:1.45; }
</style>
