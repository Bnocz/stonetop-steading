import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { fetchPlayerMap, fetchPlayerSessions, playerMapImageUrl } from '../api/map.ts'
import { mapStateAt, type MapState } from '../map/route.ts'
import MapView from './MapView.tsx'
import SessionNav from './SessionNav.tsx'
import StopList from './StopList.tsx'

const nothing: MapState = { start: null, waypoints: [], earlier: [], party: null }

// Read-only: the server only sends sessions the GM has shared.
function PlayerMap({ token }: { token: string }) {
  const map = useQuery({ queryKey: ['view', token, 'map'], queryFn: () => fetchPlayerMap(token) })
  const sessions = useQuery({ queryKey: ['view', token, 'sessions'], queryFn: () => fetchPlayerSessions(token) })
  // null follows the latest session; otherwise the player has stepped back with the arrows.
  const [selectedId, setSelectedId] = useState<string | null>(null)

  if (map.isPending || sessions.isPending) return <p>Loading the map…</p>
  if (map.isError || sessions.isError) return <p>Could not load the map.</p>

  if (!map.data.image) {
    return (
      <section>
        <h2>Map</h2>
        <p>The GM hasn't added a map yet.</p>
      </section>
    )
  }

  const list = sessions.data
  const found = list.findIndex((s) => s.id === selectedId)
  const index = found >= 0 ? found : list.length - 1
  const session = list[index]

  return (
    <section>
      <h2>Map</h2>
      {session ? (
        <SessionNav
          session={session}
          index={index}
          count={list.length}
          onSelect={(next) => setSelectedId(list[next].id)}
        />
      ) : (
        <p>No sessions yet.</p>
      )}
      <MapView
        imageUrl={playerMapImageUrl(token, map.data.image.version)}
        state={session ? mapStateAt(list, index) : nothing}
      />
      {session && <StopList stops={session.waypoints} />}
    </section>
  )
}

export default PlayerMap
