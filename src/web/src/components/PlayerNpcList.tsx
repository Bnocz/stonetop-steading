import { useQuery } from '@tanstack/react-query'
import { useId } from 'react'
import { fetchPlayerNpcs, fetchPlayerPortraits, playerPortraitUrl, type PlayerNpc } from '../api/npcs.ts'

function PlayerNpcList({ token }: { token: string }) {
  const npcs = useQuery({
    queryKey: ['view', token, 'npcs'],
    queryFn: () => fetchPlayerNpcs(token),
  })

  // Portraits are a nice-to-have: if they fail to load, the NPCs still show.
  const portraits = useQuery({
    queryKey: ['view', token, 'npc-portraits'],
    queryFn: () => fetchPlayerPortraits(token),
  })
  const versions = new Map((portraits.data ?? []).map((p) => [p.npcId, p.version]))

  if (npcs.isPending) return <p>Loading the NPCs…</p>
  if (npcs.isError) return <p>Could not load the NPCs.</p>

  return (
    <section>
      <h2>NPCs</h2>
      {npcs.data.length === 0 && <p>No NPCs yet.</p>}
      {npcs.data.map((npc) => (
        <PlayerNpcCard
          key={npc.id}
          npc={npc}
          portraitUrl={versions.has(npc.id) ? playerPortraitUrl(token, npc.id, versions.get(npc.id)!) : null}
        />
      ))}
    </section>
  )
}

function PlayerNpcCard({ npc, portraitUrl }: { npc: PlayerNpc; portraitUrl: string | null }) {
  const headingId = useId()
  return (
    <article className="npc" aria-labelledby={headingId}>
      {portraitUrl && <img className="portrait" src={portraitUrl} alt={`Portrait of ${npc.name}`} />}
      <div className="npc-body">
        <h3 id={headingId}>{npc.name}</h3>
        {npc.description && <p className="npc-text">{npc.description}</p>}
      </div>
    </article>
  )
}

export default PlayerNpcList
