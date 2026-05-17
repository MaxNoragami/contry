export const APP_URLS = {
  api: {
    developmentTarget: 'http://localhost:5087',
    production: 'https://api.contry.app',
  },
  lucideStaticBase: 'https://unpkg.com/lucide-static@latest',
} as const

export const API_PATHS = {
  auth: {
    register: '/users',
    currentUser: '/users/me',
    login: '/sessions',
    currentSession: '/sessions/current',
    refresh: '/tokens/refresh',
    xsrf: '/xsrf',
  },
  ranked: {
    challengeCurrent: '/ranked/challenges/current',
    sessionCurrent: '/ranked/sessions/current',
    sessionGiveUp: '/ranked/sessions/current/give-up',
    guesses: '/ranked/guesses',
    statsMe: '/ranked/stats/me',
  },
  leaderboards: {
    ranked: '/leaderboards/ranked',
  },
  cluePacks: {
    root: '/clue-packs',
  },
  datasets: {
    manifest: '/datasets/manifest.json',
    baseCountries: '/datasets/base/countries.csv',
  },
} as const

export const STORAGE_KEYS = {
  gameMode: 'contry.game_mode',
  clueWorkspace: 'contry.clue_workspace',
} as const

export const APP_LIMITS = {
  activeClueCount: 5,
  suggestionCount: 4,
  leaderboardPageSize: 7,
  cluePackPageSize: 8,
  iconPickerResultCount: 50,
  uploadMissingExampleCount: 3,
  toastVisibleCount: 3,
} as const

export const APP_TIMINGS = {
  modalResetMs: 300,
  guessShakeMs: 500,
  submitPreviewMs: 250,
  keyboardRefocusMs: 10,
  toastDurationMs: 3000,
} as const

export const DEFAULT_CLUE_IDS = [
  'hemisphere',
  'continent',
  'temperature_avg_c',
  'population',
  'coordinates',
] as const

export function getLucideIconUrl(iconName: string): string {
  return `${APP_URLS.lucideStaticBase}/icons/${iconName}.svg`
}

export function getLucideTagsUrl(): string {
  return `${APP_URLS.lucideStaticBase}/tags.json`
}
