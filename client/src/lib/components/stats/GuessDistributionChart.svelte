<script lang="ts">
  interface Props {
    buckets: Array<{ label: string; count: number }>
  }

  let { buckets }: Props = $props()

  const maxCount = $derived(Math.max(1, ...buckets.map((bucket) => bucket.count)))
</script>

<div class="chart">
  {#each buckets as bucket (bucket.label)}
    <div class="row">
      <div class="label">{bucket.label}</div>
      <div class="bar-track">
        {#if bucket.count > 0}
          <div class="bar-fill" style={`width:${(bucket.count / maxCount) * 100}%`}>
            <span class="bar-count">{bucket.count}</span>
          </div>
        {/if}
      </div>
    </div>
  {/each}
</div>

<style>
  .chart {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .row {
    display: grid;
    grid-template-columns: 36px 1fr;
    align-items: center;
    gap: 10px;
  }

  .label {
    font-size: 15px;
    font-weight: 700;
    color: var(--text);
    text-align: right;
  }

  .bar-track {
    min-height: 22px;
    display: flex;
    align-items: center;
  }

  .bar-fill {
    min-width: 24px;
    min-height: 22px;
    border-radius: 999px;
    background: var(--ok);
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding: 0 8px;
  }

  .bar-count {
    font-size: 13px;
    font-weight: 700;
    color: var(--chip-fg);
  }
</style>
