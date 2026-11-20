import { useState, type MouseEvent } from 'react'
import type { Point } from '../api/map.ts'
import type { MapState } from '../map/route.ts'

// Until the image loads, assume a typical landscape map. Positions are fractions, so only
// the aspect ratio matters, and the overlay is corrected as soon as the image arrives.
const DEFAULT_WIDTH = 1000
const DEFAULT_HEIGHT = 750

const round = (value: number) => Math.round(value * 10) / 10
const clamp = (value: number) => Math.min(1, Math.max(0, value))

function MapView({
  imageUrl,
  state,
  onMapClick,
}: {
  imageUrl: string
  state: MapState
  onMapClick?: (point: Point) => void
}) {
  const [size, setSize] = useState({ width: DEFAULT_WIDTH, height: DEFAULT_HEIGHT })
  const { width, height } = size
  // Marker sizes are in map units, scaled so they look the same on any map.
  const unit = width / 1000

  const at = (point: Point) => `${round(point.x * width)},${round(point.y * height)}`
  const current = state.start ? [state.start, ...state.waypoints] : state.waypoints

  function handleClick(event: MouseEvent<SVGSVGElement>) {
    if (!onMapClick) return
    const box = event.currentTarget.getBoundingClientRect()
    onMapClick({
      x: clamp((event.clientX - box.left) / box.width),
      y: clamp((event.clientY - box.top) / box.height),
    })
  }

  return (
    <div className={onMapClick ? 'map-frame editing' : 'map-frame'}>
      <img
        className="map-image"
        src={imageUrl}
        alt=""
        onLoad={(event) => {
          const { naturalWidth, naturalHeight } = event.currentTarget
          if (naturalWidth > 0 && naturalHeight > 0) {
            setSize({ width: DEFAULT_WIDTH, height: (DEFAULT_WIDTH * naturalHeight) / naturalWidth })
          }
        }}
      />
      <svg
        className="map-overlay"
        viewBox={`0 0 ${width} ${round(height)}`}
        aria-label="Region map"
        onClick={handleClick}
      >
        {state.earlier.map((route, index) => (
          <polyline
            key={index}
            className="route-earlier"
            points={route.map(at).join(' ')}
            strokeWidth={3 * unit}
            strokeDasharray={`${2 * unit} ${9 * unit}`}
          />
        ))}
        {current.length > 1 && (
          <polyline
            className="route-current"
            points={current.map(at).join(' ')}
            strokeWidth={4 * unit}
            strokeDasharray={`${14 * unit} ${8 * unit}`}
          />
        )}
        {current.slice(1).map((to, index) => {
          const from = current[index]
          const angle = (Math.atan2((to.y - from.y) * height, (to.x - from.x) * width) * 180) / Math.PI
          const middle = { x: (from.x + to.x) / 2, y: (from.y + to.y) / 2 }
          return (
            <path
              key={index}
              className="route-arrow"
              d={`M${-9 * unit},${-7 * unit} L${7 * unit},0 L${-9 * unit},${7 * unit} Z`}
              transform={`translate(${at(middle)}) rotate(${round(angle)})`}
            />
          )
        })}
        {state.party && (
          <g aria-label="The party is here" className="party">
            <circle
              className="party-ring"
              cx={round(state.party.x * width)}
              cy={round(state.party.y * height)}
              r={22 * unit}
              strokeWidth={2.5 * unit}
              strokeDasharray={`${4 * unit} ${4 * unit}`}
            />
            {state.waypoints.length === 0 && (
              <circle
                className="party-dot"
                cx={round(state.party.x * width)}
                cy={round(state.party.y * height)}
                r={11 * unit}
                strokeWidth={2 * unit}
              />
            )}
          </g>
        )}
        {state.waypoints.map((stop, index) => (
          <g key={index} aria-label={stop.label ? `Stop ${index + 1}: ${stop.label}` : `Stop ${index + 1}`}>
            <circle
              className={index === state.waypoints.length - 1 ? 'stop last' : 'stop'}
              cx={round(stop.x * width)}
              cy={round(stop.y * height)}
              r={(index === state.waypoints.length - 1 ? 13 : 11) * unit}
              strokeWidth={2 * unit}
            />
            <text
              className="stop-number"
              x={round(stop.x * width)}
              y={round(stop.y * height + 5 * unit)}
              fontSize={14 * unit}
            >
              {index + 1}
            </text>
          </g>
        ))}
      </svg>
    </div>
  )
}

export default MapView
