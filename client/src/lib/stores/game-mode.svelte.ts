export type GameMode = 'arcade' | 'ranked'

const STORAGE_KEY = 'contry.game_mode'

export function createGameModeStore() {
  const initialMode = typeof window !== 'undefined'
    ? (window.localStorage.getItem(STORAGE_KEY) as GameMode | null) ?? 'arcade'
    : 'arcade'

  let current = $state<GameMode>(initialMode === 'ranked' ? 'ranked' : 'arcade')

  function setMode(nextMode: GameMode) {
    current = nextMode
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(STORAGE_KEY, nextMode)
    }
  }

  function toggle() {
    setMode(current === 'arcade' ? 'ranked' : 'arcade')
  }

  return {
    get current() { return current },
    setMode,
    toggle,
  }
}
