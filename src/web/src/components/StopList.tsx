import type { Waypoint } from '../api/map.ts'

// The numbered key under the map: one line per stop, matching the numbers on the route.
function StopList({ stops }: { stops: Waypoint[] }) {
  if (stops.length === 0) return null

  return (
    <ol className="stop-legend" aria-label="Stops">
      {stops.map((stop, index) => (
        <li key={index}>
          {stop.label || <span className="unnamed">Unnamed stop</span>}
          {index === stops.length - 1 && <span className="here"> (the party is here)</span>}
        </li>
      ))}
    </ol>
  )
}

export default StopList
