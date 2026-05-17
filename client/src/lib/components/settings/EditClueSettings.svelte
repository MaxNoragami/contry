<script lang="ts">
  import {
    ArrowLeft,
    Image as ImageIcon,
    Save,
    Table2,
    Trash2,
    TriangleAlert,
    Upload,
    X,
  } from "lucide-svelte";
  import { fly } from "svelte/transition";
  import Papa from "papaparse";
  import { API_PATHS, APP_LIMITS } from "../../config/app";
  import { getCluePack, type CluePackDetailDto } from "../../api/client";
  import {
    canPushCloudLink,
    createCloudLinkFromDetail,
    loadClueCloudLinks,
    markClueCloudLinkDirty,
    overwriteLocalClueFromRemote,
    removeClueCloudLink,
    setClueCloudLink,
    type ClueCloudLink,
  } from "../../clues/cloud";
  import {
    loadWorkspaceCustomClues,
    loadWorkspaceSelectedClues,
    removeWorkspaceCustomRowsForClue,
    saveWorkspaceCustomClues,
    saveWorkspaceSelectedClues,
    setWorkspaceCustomRowsForClue,
  } from "../../clues/workspace";
  import { getProblemMessage } from "../../stores/auth.svelte";
  import { getDB } from "../../stores/db";
  import { toastStore } from "../../stores/toasts.svelte";
  import type { DatasetClueEntry } from "../../datasets/manifest";
  import type { createAuthStore } from "../../stores/auth.svelte";
  import type { ViewType, DraftClueData, NavDirection } from "./types";

  interface Props {
    game: any;
    auth: ReturnType<typeof createAuthStore>;
    onBack: () => void;
    onNavigate: (view: ViewType) => void;
    direction: NavDirection;
    hasUnsavedChanges?: boolean;
    discardPromptVisible?: boolean;
    newClueDraft: DraftClueData;
  }

  let {
    game,
    auth,
    onBack,
    onNavigate,
    direction,
    hasUnsavedChanges = $bindable(false),
    discardPromptVisible = $bindable(false),
    newClueDraft = $bindable(),
  }: Props = $props();

  let fileInput = $state<HTMLInputElement | undefined>();
  let uploadError = $state<string | null>(null);
  let typeWarning = $state<string | null>(null);
  let deletePromptVisible = $state(false);
  let globalDeletePromptVisible = $state(false);
  let uploadedFileName = $state<string | null>(null);
  let uploadedFileSize = $state<string | null>(null);
  let cloudBusy = $state(false);
  let cloudLink = $state<ClueCloudLink | null>(null);

  function serializeDraft() {
    return JSON.stringify({
      id: newClueDraft.id,
      label: newClueDraft.label,
      description: newClueDraft.description,
      type: newClueDraft.type,
      comparator: newClueDraft.comparator,
      unitSymbol: newClueDraft.unitSymbol,
      icon: newClueDraft.icon,
      categories: [...newClueDraft.categories],
      data: newClueDraft.data.map((r) => ({ country_id: r.country_id, value: r.value })),
    });
  }

  function restoreDraftFromBaseline() {
    if (!newClueDraft.baselineSnapshot) return;
    const parsed = JSON.parse(newClueDraft.baselineSnapshot);
    newClueDraft.id = parsed.id;
    newClueDraft.label = parsed.label;
    newClueDraft.description = parsed.description;
    newClueDraft.type = parsed.type;
    newClueDraft.comparator = parsed.comparator;
    newClueDraft.unitSymbol = parsed.unitSymbol;
    newClueDraft.icon = parsed.icon;
    newClueDraft.categories = [...parsed.categories];
    newClueDraft.data = parsed.data.map((row: { country_id: string; value: any }) => ({
      country_id: row.country_id,
      value: row.value,
    }));
  }

  $effect(() => {
    if (!newClueDraft.baselineSnapshot && newClueDraft.mode === "edit") {
      newClueDraft.baselineSnapshot = serializeDraft();
    }
  });

  $effect(() => {
    if (!newClueDraft.id) return;
    getDB().then(async (db) => {
      const links = await loadClueCloudLinks(db);
      cloudLink = links[newClueDraft.id] || null;
    });
  });

  const serializedDraft = $derived.by(() =>
    JSON.stringify({
      id: newClueDraft.id,
      label: newClueDraft.label,
      description: newClueDraft.description,
      type: newClueDraft.type,
      comparator: newClueDraft.comparator,
      unitSymbol: newClueDraft.unitSymbol,
      icon: newClueDraft.icon,
      categories: [...newClueDraft.categories],
      data: newClueDraft.data.map((r) => ({ country_id: r.country_id, value: r.value })),
    }),
  );

  const isDirty = $derived(!!newClueDraft.baselineSnapshot && serializedDraft !== newClueDraft.baselineSnapshot);
  const canPushLink = $derived(canPushCloudLink(cloudLink, auth.user?.id, auth.user?.role));
  const isSaveDisabled = $derived(!isDirty || !newClueDraft.id || !newClueDraft.label || !newClueDraft.description.trim() || newClueDraft.data.length === 0);
  const missingDataCount = $derived.by(() => newClueDraft.data.filter((d) => d.value === null || d.value === undefined || d.value === "").length);

  $effect(() => {
    hasUnsavedChanges = isDirty;
  });

  $effect(() => {
    if (!newClueDraft.data.length) return;
    recomputeDraftType(false);
  });

  function handleBack() {
    if (isDirty) discardPromptVisible = true;
    else onBack();
  }

  function clearUploadedFile() {
    newClueDraft.data = [];
    newClueDraft.type = "numeric";
    newClueDraft.comparator = "higher_lower";
    newClueDraft.categories = [];
    uploadedFileName = null;
    uploadedFileSize = null;
    uploadError = null;
    typeWarning = null;
  }

  function recomputeDraftType(showWarning = false) {
    let hasString = false;
    const uniqueValues = new Set<string>();
    for (const row of newClueDraft.data) {
      const raw = row.value;
      if (raw === null || raw === undefined || raw === "") continue;
      if (typeof raw === "number") continue;
      const text = String(raw).trim();
      if (!text) continue;
      const parsed = Number(text);
      if (Number.isNaN(parsed)) {
        hasString = true;
        uniqueValues.add(text);
      }
    }
    if (hasString) {
      const wasNumeric = newClueDraft.type === "numeric";
      newClueDraft.type = "categorical";
      newClueDraft.comparator = "exact";
      newClueDraft.categories = Array.from(uniqueValues);
      if (showWarning && wasNumeric) {
        typeWarning = "Text values were detected. This clue is now treated as Categorical.";
      }
    } else {
      newClueDraft.type = "numeric";
      newClueDraft.comparator = "higher_lower";
      newClueDraft.categories = [];
    }
  }

  function handleFileSelect(e: Event) {
    const target = e.target as HTMLInputElement;
    if (!target.files || target.files.length === 0) return;
    const file = target.files[0];
    uploadError = null;
    typeWarning = null;

    const fileSize = file.size;
    uploadedFileSize =
      fileSize < 1024
        ? `${fileSize} B`
        : fileSize < 1024 * 1024
          ? `${(fileSize / 1024).toFixed(1)} KB`
          : `${(fileSize / (1024 * 1024)).toFixed(1)} MB`;

    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target?.result as string;
      if (file.name.endsWith(".json")) {
        try {
          const json = JSON.parse(content);
          if (Array.isArray(json)) processDataRows(json, file.name);
        } catch {
          uploadError = "Invalid JSON file. Could not parse.";
        }
      } else {
        Papa.parse(content, {
          header: true,
          skipEmptyLines: true,
          complete: (results) => {
            if (results.data.length === 0) return;
            let processed: any[] = [];
            const fields = results.meta.fields || [];
            if (fields.includes("country_id") && fields.includes("value")) {
              processed = results.data;
            } else {
              Papa.parse(content, {
                header: false,
                skipEmptyLines: true,
                complete: (res2) => {
                  processed = res2.data.map((row: any) => ({ country_id: row[0], value: row[1] }));
                  processDataRows(processed, file.name);
                },
              });
              return;
            }
            processDataRows(processed, file.name);
          },
        });
      }
    };
    reader.readAsText(file);
    target.value = "";
  }

  function processDataRows(rows: any[], fileName: string) {
    const poolIds = new Set(game.countryPool.map((c: any) => c.country_id));
    const uploadedIds = new Set(rows.map((r) => r.country_id));
    const missingCodes = Array.from(poolIds).filter((id) => !uploadedIds.has(id as string));
    if (missingCodes.length > 0) {
      uploadError = `The document is missing rows for ${missingCodes.length} countries (e.g., ${missingCodes.slice(0, APP_LIMITS.uploadMissingExampleCount).join(", ")}). Please use the template.`;
      return;
    }
    const validRows: { country_id: string; value: any }[] = [];
    for (const row of rows) {
      if (!row.country_id || !poolIds.has(row.country_id)) continue;
      let val = row.value;
      if (val === undefined || val === null || val === "") val = null;
      else if (typeof val === "string") {
        const parsed = Number(val);
        val = Number.isNaN(parsed) ? String(val).trim() : parsed;
      }
      validRows.push({ country_id: row.country_id, value: val });
    }
    newClueDraft.data = validRows;
    uploadedFileName = fileName;
    recomputeDraftType(true);
    onNavigate("dataset-editor");
  }

  async function handleSave() {
    if (isSaveDisabled) return;
    const db = await getDB();
    const existingClues = await loadWorkspaceCustomClues(db)
    const index = existingClues.findIndex((c) => c.id === newClueDraft.id);
    if (index === -1) return;

    existingClues[index] = {
      ...existingClues[index],
      type: newClueDraft.type,
      label: newClueDraft.label,
      description: newClueDraft.description.trim(),
      icon: newClueDraft.icon,
      unit_symbol: newClueDraft.unitSymbol,
      categories: newClueDraft.type === "categorical" ? [...newClueDraft.categories] : undefined,
    };
    await saveWorkspaceCustomClues(db, existingClues.map((c) => ({ ...c, categories: c.categories ? [...c.categories] : undefined })));

    await setWorkspaceCustomRowsForClue(db, newClueDraft.id, newClueDraft.data.map((row) => ({
      country_id: row.country_id,
      value: row.value ?? null,
    })));

    await markClueCloudLinkDirty(db, newClueDraft.id);

    if (auth.isAuthenticated && (!cloudLink || canPushLink)) {
      try {
        const body = {
          datasetId: newClueDraft.id,
          label: newClueDraft.label,
          description: newClueDraft.description.trim(),
          type: newClueDraft.type,
          comparator: newClueDraft.comparator,
          unitSymbol: newClueDraft.type === "numeric" && newClueDraft.unitSymbol.trim() ? newClueDraft.unitSymbol.trim() : null,
          icon: newClueDraft.icon,
          categories: newClueDraft.type === "categorical" ? [...newClueDraft.categories] : [],
          rows: newClueDraft.data.map((row) => ({ countryId: row.country_id, value: row.value ?? null })),
          visibility: cloudLink?.visibility || 'public',
        }
        const wasPublished = !!cloudLink
        const detail = cloudLink
          ? await auth.request<CluePackDetailDto>(`${API_PATHS.cluePacks.root}/${cloudLink.remoteId}`, {
              method: 'PUT',
              body,
            })
          : await auth.request<CluePackDetailDto>(API_PATHS.cluePacks.root, {
              method: 'POST',
              body,
            })
        const nextLink = createCloudLinkFromDetail(detail)
        await setClueCloudLink(db, newClueDraft.id, nextLink)
        cloudLink = nextLink
        toastStore.push(wasPublished ? 'Clue saved and synced to the cloud.' : 'Clue saved and published to the cloud.', 'success')
      } catch (error) {
        toastStore.push(getProblemMessage(error))
      }
    }

    await game.refreshCustomClueCatalog();
    newClueDraft.baselineSnapshot = serializedDraft;
    discardPromptVisible = false;
    onBack();
  }

  async function handleDeleteGlobal() {
    if (!cloudLink || !canPushLink || cloudBusy) return;
    cloudBusy = true;
    try {
      const db = await getDB();
      await auth.request<void>(`${API_PATHS.cluePacks.root}/${cloudLink.remoteId}`, { method: 'DELETE' });
      await removeClueCloudLink(db, newClueDraft.id);
      cloudLink = null;
      deletePromptVisible = false;
      globalDeletePromptVisible = false;
      await handleDeleteLocal(true);
      toastStore.push("Published clue deleted from the cloud.", 'success');
      onBack();
    } catch (error) {
      toastStore.push(getProblemMessage(error));
    } finally {
      cloudBusy = false;
    }
  }

  async function handleDeleteLocal(skipClose = false) {
    const deleteId = newClueDraft.id;
    const db = await getDB();
    const existingClues = await loadWorkspaceCustomClues(db)
    await saveWorkspaceCustomClues(db, existingClues.filter((c) => c.id !== deleteId));
    const selected = await loadWorkspaceSelectedClues(db)
    let nextSelected = Array.isArray(selected) ? selected.filter((id: string) => id !== deleteId) : [];
    const availableIds = game.availableClues.map((c: any) => c.id).filter((id: string) => id !== deleteId);
    for (const id of availableIds) {
      if (nextSelected.length >= 5) break;
      if (!nextSelected.includes(id)) nextSelected.push(id);
    }
    for (const id of ["hemisphere", "continent", "temperature_avg_c", "population", "coordinates"]) {
      if (nextSelected.length >= 5) break;
      if (id !== deleteId && !nextSelected.includes(id)) nextSelected.push(id);
    }
    await saveWorkspaceSelectedClues(db, nextSelected.slice(0, 5));

    await removeWorkspaceCustomRowsForClue(db, deleteId)

    await removeClueCloudLink(db, deleteId);
    await game.refreshCustomClueCatalog();
    hasUnsavedChanges = false;
    if (!skipClose) {
      onBack();
    }
  }

  function applyCloudDetailToDraft(detail: Awaited<ReturnType<typeof getCluePack>>) {
    newClueDraft.label = detail.label;
    newClueDraft.description = detail.description;
    newClueDraft.type = detail.type;
    newClueDraft.comparator = detail.comparator;
    newClueDraft.unitSymbol = detail.unitSymbol || "";
    newClueDraft.icon = detail.icon;
    newClueDraft.categories = [...detail.categories];
    newClueDraft.data = detail.rows.map((row) => ({ country_id: row.countryId, value: row.value ?? null }));
    newClueDraft.baselineSnapshot = serializeDraft();
  }
</script>

<div class="view-container" in:fly={{ x: direction === "back" ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === "back" ? 20 : -20, duration: 200 }}>
  {#if discardPromptVisible || deletePromptVisible || globalDeletePromptVisible}
    <div class="warning-view" in:fly={{ x: direction === "back" ? -20 : 20, duration: 250 }} out:fly={{ x: direction === "back" ? 20 : -20, duration: 200 }}>
      <div class="modal-header">
        <h2 class="warning-title">Warning</h2>
      </div>
      <div class="warning-body">
        <div class="warning-icon"><TriangleAlert size={28} /></div>
        <p class="warning-text">
          {#if globalDeletePromptVisible}
            Delete this published clue globally? This removes the cloud version and your local copy.
          {:else if deletePromptVisible}
            Delete this custom clue and its local dataset from this browser?
          {:else}
            You have unsaved changes. Discard them?
          {/if}
        </p>
        <div class="warning-actions">
          <button class="warning-btn muted" onclick={() => { discardPromptVisible = false; deletePromptVisible = false; globalDeletePromptVisible = false; }}>Keep Editing</button>
          {#if globalDeletePromptVisible}
            <button class="warning-btn danger" onclick={handleDeleteGlobal}>Delete Globally</button>
          {:else if deletePromptVisible}
            <button class="warning-btn danger" onclick={() => handleDeleteLocal()}>Delete Locally</button>
          {:else}
            <button class="warning-btn danger" onclick={() => { restoreDraftFromBaseline(); onBack(); }}>Discard</button>
          {/if}
        </div>
      </div>
    </div>
  {:else if uploadError}
    <div class="warning-view" in:fly={{ x: direction === "back" ? -20 : 20, duration: 250 }} out:fly={{ x: direction === "back" ? 20 : -20, duration: 200 }}>
      <div class="modal-header">
        <h2 class="warning-title">Warning</h2>
      </div>
      <div class="warning-body">
        <div class="warning-icon"><TriangleAlert size={28} /></div>
        <p class="warning-text">{uploadError}</p>
        <div class="warning-actions single-action">
          <button class="warning-btn muted" onclick={() => (uploadError = null)}>Dismiss</button>
        </div>
      </div>
    </div>
  {:else}
    <div class="modal-header">
      <button class="icon-btn back-btn" aria-label="Back" onclick={handleBack}><ArrowLeft /></button>
      <h2 class="centered-title">Edit Custom Clue</h2>
      <div class="header-actions">
        <button class="icon-btn save-btn" class:is-ready={!isSaveDisabled} aria-label="Save" onclick={handleSave} disabled={isSaveDisabled}><Save /></button>
      </div>
    </div>

    <div class="modal-body form-body">
      {#if typeWarning}
        <div class="inline-dialog warning"><TriangleAlert size={18} /><span>{typeWarning}</span><div class="dialog-actions"><button class="dialog-btn muted" onclick={() => { typeWarning = null; onNavigate("dataset-editor"); }}>Keep Editing</button><button class="dialog-btn accent" onclick={() => (typeWarning = null)}>Proceed</button></div></div>
      {/if}

      <div class="menu-actions">
        {#if uploadedFileName}
          <button class="action-btn" onclick={clearUploadedFile}><div class="action-icon danger"><X size={20} /></div><div class="action-text"><span>{uploadedFileName}</span><span class="muted">{newClueDraft.data.length} records · {uploadedFileSize}</span></div></button>
        {:else}
          <button class="action-btn" onclick={() => fileInput?.click()}><div class="action-icon accent"><Upload size={20} /></div><div class="action-text"><span>Import Data</span><span class="muted">CSV or JSON file</span></div></button>
        {/if}
      </div>

      <input type="file" bind:this={fileInput} onchange={handleFileSelect} accept=".csv,text/csv,text/plain,.json,application/json" style="display:none;" />

      <div class="form-group"><span class="fake-label">Dataset ID</span><div class="readonly-value">{newClueDraft.id}</div></div>
      <div class="form-group"><label for="clue-label">Label</label><input id="clue-label" type="text" bind:value={newClueDraft.label} /></div>
      <div class="form-group"><div class="field-header"><label for="clue-description">Description</label><span class="field-counter">{newClueDraft.description.length}/120</span></div><textarea id="clue-description" bind:value={newClueDraft.description} rows="2" maxlength="120" placeholder="Short explanation shown in clue details"></textarea></div>
      <div class="form-group row"><div class="half"><span class="fake-label">Type</span><div class="readonly-value">{newClueDraft.type}</div></div><div class="half"><span class="fake-label">Comparator</span><div class="readonly-value">{newClueDraft.comparator}</div></div></div>
      {#if newClueDraft.type === "numeric"}
        <div class="form-group"><label for="clue-unit">Unit Symbol (Optional)</label><input id="clue-unit" type="text" bind:value={newClueDraft.unitSymbol} placeholder="e.g. $, %, km²" /></div>
      {/if}

      <div class="menu-actions">
        <button class="action-btn" onclick={() => onNavigate("icon-picker")}><div class="action-icon"><ImageIcon size={18} /></div><div class="action-text"><span>Icon</span><span class="muted">{newClueDraft.icon}</span></div></button>
        <button class="action-btn" onclick={() => onNavigate("dataset-editor")} disabled={newClueDraft.data.length === 0}><div class="action-icon"><Table2 size={18} /></div><div class="action-text"><span>View/Edit Data</span><span class="muted">{newClueDraft.data.length} records{#if missingDataCount > 0} · {missingDataCount} empty{/if}</span></div></button>
        {#if cloudLink && canPushLink}
          <button class="action-btn delete-action" onclick={() => (globalDeletePromptVisible = true)}><div class="action-icon danger"><Trash2 size={18} /></div><div class="action-text"><span>Delete Globally</span><span class="muted">Remove the cloud version and this local copy</span></div></button>
        {/if}
        <button class="action-btn delete-action" onclick={() => (deletePromptVisible = true)}><div class="action-icon danger"><Trash2 size={18} /></div><div class="action-text"><span>Delete Locally</span><span class="muted">Remove this browser copy only</span></div></button>
      </div>
    </div>
  {/if}
</div>

<style>
  .view-container { position:absolute; inset:0; display:flex; flex-direction:column; width:100%; height:100%; overflow-y:auto; scrollbar-width:none; -ms-overflow-style:none; }
  .view-container::-webkit-scrollbar { display:none; }
  .modal-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; background:var(--panel); position:sticky; top:0; z-index:2; }
  .centered-title { position:absolute; left:50%; transform:translateX(-50%); font-size:18px; font-weight:500; margin:0; }
  .header-actions { display:flex; gap:8px; }
  .icon-btn { width:40px; height:40px; border-radius:50%; border:none; background:transparent; color:var(--text); display:grid; place-items:center; cursor:pointer; transition:background .2s, box-shadow .2s, color .2s; outline:none; }
  @media (hover:hover) { .icon-btn:hover:not(:disabled) { background:var(--hover-strong); } }
  .icon-btn:active:not(:disabled) { background:var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }
  .icon-btn:disabled { pointer-events:none; box-shadow:none; }
  .save-btn { color:var(--border-strong); }
  .save-btn.is-ready { color:var(--accent); }
  .form-body { padding:16px 20px; display:flex; flex-direction:column; gap:20px; }
  .inline-dialog { display:flex; flex-direction:column; gap:10px; padding:14px 16px; background:var(--field-bg); border-radius:12px; font-size:14px; color:var(--text); }
  .inline-dialog.warning { background: color-mix(in oklab, var(--warn) 10%, var(--panel)); border:1px solid var(--warn); color:var(--warn); }
  .dialog-actions { display:flex; gap:8px; align-self:flex-end; }
  .dialog-btn { padding:8px 16px; border-radius:8px; border:none; font-size:13px; cursor:pointer; }
  .dialog-btn.muted { background:var(--border-strong); color:var(--text); }
  .dialog-btn.accent { background:var(--info); color:var(--text); }
  .menu-actions { display:flex; flex-direction:column; background:var(--panel); border-radius:12px; overflow:hidden; border:1px solid var(--border); }
  .action-btn { display:flex; align-items:center; gap:16px; padding:16px; background:transparent; border:none; color:var(--text); text-align:left; cursor:pointer; border-bottom:1px solid var(--border); }
  .action-btn:last-child { border-bottom:none; }
  .action-btn:disabled { opacity:0.4; cursor:not-allowed; }
  @media (hover:hover) { .action-btn:hover:not(:disabled) { background:var(--hover-soft);} .delete-action:hover:not(:disabled) { background: color-mix(in oklab, var(--bad) 12%, var(--panel));} }
  .action-btn:active:not(:disabled) { background:var(--hover-soft); }
  .action-btn:focus-visible:not(:disabled) { outline:none; box-shadow: inset 0 0 0 1px var(--info); }
  .delete-action:active { background: color-mix(in oklab, var(--bad) 12%, var(--panel)); }
  .action-icon { color:var(--muted); flex-shrink:0; }
  .action-icon.accent { color:var(--info); }
  .action-icon.danger { color:var(--bad); }
  .action-text { display:flex; flex-direction:column; gap:2px; }
  .action-text span { font-size:15px; font-weight:500; }
  .action-text .muted { font-size:13px; color:var(--muted); font-weight:400; }
  .form-group { display:flex; flex-direction:column; gap:8px; }
  .field-header { display:flex; align-items:center; justify-content:space-between; gap:12px; }
  .field-counter { font-size:12px; color:var(--muted); }
  .form-group.row { flex-direction:row; gap:16px; }
  .half { flex:1; display:flex; flex-direction:column; gap:8px; }
  label,.fake-label { font-size:13px; color:var(--muted); font-weight:500; }
  input[type="text"] { background:var(--field-bg); border:1px solid var(--field-border); padding:12px; border-radius:8px; color:var(--text); font-size:15px; outline:none; }
  input[type="text"]:focus { border-color:var(--info); }
  textarea { background:var(--field-bg); border:1px solid var(--field-border); padding:12px; border-radius:8px; color:var(--text); font-size:15px; line-height:1.5; outline:none; resize:none; min-height:calc(1.5em * 2 + 24px); font-family:inherit; }
  textarea:focus { border-color:var(--info); }
  .readonly-value { background:var(--field-bg); border:1px solid var(--field-border); padding:12px; border-radius:8px; color:var(--muted); font-size:15px; text-transform:capitalize; }
  .warning-view { position:absolute; inset:0; display:flex; flex-direction:column; background:var(--panel); }
  .warning-title { margin:0 auto; font-size:18px; font-weight:500; }
  .warning-body { flex:1; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:20px; padding:24px; text-align:center; }
  .warning-icon { color:var(--warn); }
  .warning-text { margin:0; font-size:16px; color:var(--text); line-height:1.4; max-width:280px; }
  .warning-actions { display:flex; gap:12px; width:100%; max-width:320px; }
  .warning-actions.single-action { max-width:220px; }
  .warning-btn { flex:1; min-height:44px; border:none; border-radius:999px; padding:0 18px; font-size:14px; font-weight:600; cursor:pointer; transition:background .15s,color .15s; }
  .warning-btn.muted { background:var(--field-bg); color:var(--text); }
  .warning-btn.danger { background: color-mix(in oklab, var(--bad) 30%, var(--panel)); color:var(--chip-bg); }
  @media (hover:hover) { .warning-btn.muted:hover { background:var(--border-strong); } .warning-btn.danger:hover { background: color-mix(in oklab, var(--bad) 42%, var(--panel)); } }
  .warning-btn:active { transform:translateY(1px); }
</style>
