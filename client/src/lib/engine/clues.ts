import type { ChipTone, ClueResult } from '../stores/game.svelte'

export function evaluateCategorical(guessVal: any, targetVal: any, label: string): ClueResult {
  const guessMissing = guessVal === undefined || guessVal === null || guessVal === ''
  const targetMissing = targetVal === undefined || targetVal === null || targetVal === ''

  if (guessMissing) {
    return {
      clue: label,
      value: 'NO DATA',
      tone: targetMissing ? 'green' : 'red',
      kind: 'text'
    }
  }
  const isMatch = targetMissing
    ? false
    : String(guessVal).trim().toLowerCase() === String(targetVal).trim().toLowerCase()
  return {
    clue: label,
    value: String(guessVal),
    tone: isMatch ? 'green' : 'red',
    kind: 'text'
  }
}

export function evaluateNumeric(guessVal: any, targetVal: any, label: string, unitSymbol?: string): ClueResult {
  const guessMissing = guessVal === undefined || guessVal === null || guessVal === ''
  const targetMissing = targetVal === undefined || targetVal === null || targetVal === ''

  if (guessMissing && targetMissing) {
    return { clue: label, value: 'NO DATA', tone: 'green', kind: 'text' }
  }

  if (guessMissing) {
    return {
      clue: label,
      value: 'NO DATA',
      tone: 'red',
      kind: 'text',
      trend: targetMissing ? undefined : 'higher'
    }
  }

  if (targetMissing) {
    return {
      clue: label,
      value: formatNumeric(Number(guessVal), unitSymbol),
      tone: 'red',
      kind: 'numeric',
      trend: 'lower'
    }
  }

  const g = Number(guessVal)
  const t = Number(targetVal)

  if (isNaN(g) || isNaN(t)) {
    return { clue: label, value: String(guessVal), tone: 'red', kind: 'numeric' }
  }

  const isExact = g === t
  const trend = isExact ? undefined : (g > t ? 'lower' : 'higher')

  const errorPct = Math.abs(g - t) / Math.max(Math.abs(t), 1e-9) * 100

  let tone: ChipTone = 'red'
  if (errorPct <= 10) {
    tone = 'green'
  } else if (errorPct <= 35) {
    tone = 'yellow'
  }

  return {
    clue: label,
    value: formatNumeric(g, unitSymbol),
    tone,
    kind: 'numeric',
    trend
  }
}

export function evaluateHemisphere(guessLat: number, targetLat: number): ClueResult {
  const guessHemi = guessLat >= 0 ? 'NORTHERN' : 'SOUTHERN'
  const targetHemi = targetLat >= 0 ? 'NORTHERN' : 'SOUTHERN'

  return {
    clue: 'Hemisphere',
    value: guessHemi,
    tone: guessHemi === targetHemi ? 'green' : 'red',
    kind: 'text'
  }
}

export function evaluateCoordinates(
  guessLat: number, guessLon: number, 
  targetLat: number, targetLon: number
): ClueResult {
  if (guessLat === targetLat && guessLon === targetLon) {
    return {
      clue: 'Coordinates',
      value: '✓',
      tone: 'green',
      kind: 'direction'
    }
  }

  const latDelta = targetLat - guessLat
  const lonDelta = normalizeLongitudeDelta(targetLon - guessLon)
  const direction = deltasToDirection(latDelta, lonDelta)

  return {
    clue: 'Coordinates',
    value: direction,
    tone: 'blue',
    kind: 'direction'
  }
}

function normalizeLongitudeDelta(delta: number): number {
  return ((delta + 540) % 360) - 180
}

function deltasToDirection(latDelta: number, lonDelta: number): string {
  const latMagnitude = Math.abs(latDelta)
  const lonMagnitude = Math.abs(lonDelta)

  if (latMagnitude === 0) return lonDelta > 0 ? 'E' : 'W'
  if (lonMagnitude === 0) return latDelta > 0 ? 'N' : 'S'

  const minorAxisRatio = Math.min(latMagnitude, lonMagnitude) / Math.max(latMagnitude, lonMagnitude)
  if (minorAxisRatio < 0.3) {
    return latMagnitude > lonMagnitude
      ? (latDelta > 0 ? 'N' : 'S')
      : (lonDelta > 0 ? 'E' : 'W')
  }

  const latDir = latDelta > 0 ? 'N' : 'S'
  const lonDir = lonDelta > 0 ? 'E' : 'W'
  return `${latDir}${lonDir}`
}

export function formatNumeric(val: number, unitSymbol?: string): string {
  // Temperature-style units: always show one decimal, no abbreviation
  if (unitSymbol === 'degC') {
    return `${val.toFixed(1)} °C`
  }

  // Large number abbreviation for everything else
  let numStr: string
  const absVal = Math.abs(val)
  if (absVal >= 1e9) numStr = `${(val / 1e9).toFixed(1)}B`
  else if (absVal >= 1e6) numStr = `${(val / 1e6).toFixed(1)}M`
  else if (absVal >= 1e3) numStr = `${(val / 1e3).toFixed(1)}K`
  else numStr = val.toLocaleString()

  if (unitSymbol) {
    return `${numStr} ${unitSymbol}`
  }
  return numStr
}
