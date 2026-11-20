import type { Point, Waypoint } from '../api/map.ts'

export interface MapState {
  // Where the party was when the selected session began; null if they hadn't been placed yet.
  start: Point | null
  // The selected session's own stops, numbered from 1 on the map.
  waypoints: Waypoint[]
  // Each earlier session's route, from where it started to where it ended.
  earlier: Point[][]
  // Where the party is after the selected session.
  party: Point | null
}

interface SessionRoute {
  waypoints: Waypoint[]
}

const toPoint = ({ x, y }: Point): Point => ({ x, y })

// The map as it stood after the session at `index` (sessions sorted by number).
// `draft` replaces that session's waypoints while the GM is editing its route.
export function mapStateAt(sessions: SessionRoute[], index: number, draft?: Waypoint[]): MapState {
  const earlier: Point[][] = []
  let party: Point | null = null

  for (let i = 0; i < index; i++) {
    const stops = sessions[i].waypoints.map(toPoint)
    if (stops.length === 0) continue
    const route = party ? [party, ...stops] : stops
    if (route.length > 1) earlier.push(route)
    party = stops[stops.length - 1]
  }

  const start = party
  const waypoints = draft ?? sessions[index]?.waypoints ?? []
  const last = waypoints[waypoints.length - 1]
  return { start, waypoints, earlier, party: last ? toPoint(last) : start }
}
