import { useQuery } from '@tanstack/react-query'
import { useId } from 'react'
import { fetchPlayerNpcs, type PlayerNpc } from '../api/npcs.ts'

function PlayerNpcList({ token }: { token: string }) {
  const npcs = useQuery({
    queryKey: ['view', token, 'npcs'],
    queryFn: () => fetchPlayerNpcs(token),
  })

  if (npcs.isPending) return <p>Loading the NPCs…</p>
  if (npcs.isError) return <p>Could not load the NPCs.</p>

  return (
    <section>
      <h2>NPCs</h2>
      {npcs.data.length === 0 && <p>No NPCs yet.</p>}
      {npcs.data.map((npc) => (
        <PlayerNpcCard key={npc.id} npc={npc} />
      ))}
    </section>
  )
}

function PlayerNpcCard({ npc }: { npc: PlayerNpc }) {
  const headingId = useId()
  return (
    <article className="npc" aria-labelledby={headingId}>
      <h3 id={headingId}>{npc.name}</h3>
      {npc.description && <p className="npc-text">{npc.description}</p>}
    </article>
  )
}

export default PlayerNpcList
