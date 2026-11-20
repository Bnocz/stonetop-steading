import type { ReactNode } from 'react'
import { sessionHeading } from '../api/map.ts'

// ‹ Session 3: The Ruined Tower · 3 of 3 ›
function SessionNav({
  session,
  index,
  count,
  locked = false,
  onSelect,
  children,
}: {
  session: { number: number; title: string }
  index: number
  count: number
  // True while the GM is editing, so the selected session can't change underneath.
  locked?: boolean
  onSelect: (index: number) => void
  children?: ReactNode
}) {
  return (
    <nav className="session-nav" aria-label="Sessions">
      <button
        type="button"
        className="session-arrow"
        aria-label="Previous session"
        disabled={index <= 0 || locked}
        onClick={() => onSelect(index - 1)}
      >
        ‹
      </button>
      <div className="session-title">
        <h3>{sessionHeading(session)}</h3>
        <p>
          {index + 1} of {count}
        </p>
        {children}
      </div>
      <button
        type="button"
        className="session-arrow"
        aria-label="Next session"
        disabled={index >= count - 1 || locked}
        onClick={() => onSelect(index + 1)}
      >
        ›
      </button>
    </nav>
  )
}

export default SessionNav
