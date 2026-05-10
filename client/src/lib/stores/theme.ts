import { getDB } from './db'

export type ThemeMode = 'system' | 'dark' | 'light'

const THEME_KEY = 'theme_mode'

let currentThemeMode: ThemeMode = 'dark'
let systemListenerAttached = false

function normalizeThemeMode(value: unknown): ThemeMode {
  return value === 'system' || value === 'dark' || value === 'light' ? value : 'dark'
}

function getResolvedTheme(mode: ThemeMode): 'dark' | 'light' {
  if (mode === 'dark' || mode === 'light') return mode
  if (typeof window === 'undefined') return 'dark'
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

export function getThemeModeSync(): ThemeMode {
  return currentThemeMode
}

export function getThemeModeLabel(mode: ThemeMode): string {
  if (mode === 'system') return 'System'
  if (mode === 'light') return 'Light'
  return 'Dark'
}

export function cycleThemeMode(mode: ThemeMode): ThemeMode {
  if (mode === 'system') return 'dark'
  if (mode === 'dark') return 'light'
  return 'system'
}

export function applyTheme(mode: ThemeMode) {
  currentThemeMode = normalizeThemeMode(mode)

  if (typeof document === 'undefined') return

  const resolvedTheme = getResolvedTheme(currentThemeMode)
  document.documentElement.dataset.themeMode = currentThemeMode
  document.documentElement.dataset.theme = resolvedTheme
  document.documentElement.style.colorScheme = resolvedTheme
}

export async function initTheme() {
  if (typeof window === 'undefined') return currentThemeMode

  if (!systemListenerAttached) {
    const media = window.matchMedia('(prefers-color-scheme: light)')
    const handleChange = () => {
      if (currentThemeMode === 'system') applyTheme('system')
    }

    media.addEventListener('change', handleChange)
    systemListenerAttached = true
  }

  const db = await getDB()
  const storedMode = await db.transaction('settings', 'readonly').objectStore('settings').get(THEME_KEY)
  const nextMode = normalizeThemeMode(storedMode)
  applyTheme(nextMode)
  return nextMode
}

export async function setThemeMode(mode: ThemeMode) {
  const nextMode = normalizeThemeMode(mode)
  applyTheme(nextMode)

  const db = await getDB()
  const tx = db.transaction('settings', 'readwrite')
  await tx.objectStore('settings').put(nextMode, THEME_KEY)
  await tx.done

  return nextMode
}
