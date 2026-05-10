const DEG = Math.PI / 180

/**
 * Compute the initial bearing from point A to point B (in degrees, 0-360).
 */
export function bearing(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number,
): number {
  const φ1 = lat1 * DEG
  const φ2 = lat2 * DEG
  const Δλ = (lon2 - lon1) * DEG

  const y = Math.sin(Δλ) * Math.cos(φ2)
  const x =
    Math.cos(φ1) * Math.sin(φ2) -
    Math.sin(φ1) * Math.cos(φ2) * Math.cos(Δλ)

  return ((Math.atan2(y, x) / DEG) + 360) % 360
}

const DIRECTIONS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'] as const
export type CardinalDirection = (typeof DIRECTIONS)[number]

/**
 * Map a bearing (0-360) to one of 8 cardinal/intercardinal directions.
 */
export function directionFromBearing(b: number): CardinalDirection {
  const idx = Math.round(b / 45) % 8
  return DIRECTIONS[idx]
}
