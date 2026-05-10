export type ClueSource = 'builtin' | 'custom'
export type ClueType = 'numeric' | 'categorical' | 'computed'

export type DatasetBaseEntry = {
  dataset_id: 'countries_base' | string
  format: 'csv' | 'json'
  path: string
  checksum: `sha256:${string}`
}

export type DatasetClueEntry = {
  id: string
  dataset_id: string
  source: ClueSource
  type: ClueType
  computed: boolean
  metadata_path: string
  metadata_checksum: `sha256:${string}`
  format?: 'csv' | 'json'
  data_path?: string
  data_checksum?: `sha256:${string}`
  group?: string
  month?: number
  icon?: string
  label?: string
  description?: string
  unit_symbol?: string
  comparator?: 'higher_lower' | 'exact'
  categories?: string[]
}

export type DatasetManifest = {
  schema_version: string
  data_version: string
  generated_at: string
  fingerprint: `sha256:${string}`
  base: DatasetBaseEntry
  clues: DatasetClueEntry[]
}

export function isMonthlyTemperatureClue(clue: DatasetClueEntry): boolean {
  return clue.group === 'temperature_avg_c' && Number.isInteger(clue.month)
}

export function resolveTemperatureClueForMonth(
  manifest: DatasetManifest,
  month: number
): DatasetClueEntry | undefined {
  if (month < 1 || month > 12) return undefined
  return manifest.clues.find(
    (clue) => clue.group === 'temperature_avg_c' && clue.month === month
  )
}
