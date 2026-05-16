import { STORAGE_KEYS } from '../config/app'

export type GameMode = 'arcade' | 'ranked'

export function createGameModeStore() {
  const initialMode = typeof window !== 'undefined'
    ? (window.localStorage.getItem(STORAGE_KEYS.gameMode) as GameMode | null) ?? 'arcade'
    : 'arcade'

  let current = $state<GameMode>(initialMode === 'ranked' ? 'ranked' : 'arcade')

  function setMode(nextMode: GameMode) {
    current = nextMode
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(STORAGE_KEYS.gameMode, nextMode)
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
