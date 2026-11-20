import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type ReactNode } from 'react'
import {
  archiveNpc,
  createNpc,
  fetchNpcs,
  restoreNpc,
  updateNpc,
  type Npc,
  type NpcFields,
  gmPortraitUrl,
} from '../api/npcs.ts'
import NpcForm from './NpcForm.tsx'
import NpcPortraitEditor from './NpcPortraitEditor.tsx'

// null: no form open; 'new': adding; otherwise the id of the NPC being edited.
type Editing = null | 'new' | string

function NpcManager() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Editing>(null)
  const archivedHeadingId = useId()
  const npcs = useQuery({ queryKey: ['npcs'], queryFn: fetchNpcs })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['npcs'] })
  const save = useMutation({
    mutationFn: ({ id, fields }: { id: string | null; fields: NpcFields }) =>
      id === null ? createNpc(fields) : updateNpc(id, fields),
    onSuccess: async () => {
      setEditing(null)
      await refresh()
    },
  })
  const archive = useMutation({ mutationFn: archiveNpc, onSettled: refresh })
  const restore = useMutation({ mutationFn: restoreNpc, onSettled: refresh })

  function open(next: Editing) {
    save.reset()
    setEditing(next)
  }

  function form(heading: string, id: string | null, initial?: NpcFields) {
    return (
      <NpcForm
        heading={heading}
        initial={initial}
        saving={save.isPending}
        error={save.isError ? save.error.message : null}
        onSave={(fields) => save.mutate({ id, fields })}
        onCancel={() => open(null)}
      />
    )
  }

  if (npcs.isPending) return <p>Loading the NPCs…</p>
  if (npcs.isError) return <p>Could not load the NPCs.</p>

  const active = npcs.data.filter((npc) => !npc.archived)
  const archived = npcs.data.filter((npc) => npc.archived)
  const busy = archive.isPending || restore.isPending
  const actionError = archive.error ?? restore.error

  return (
    <section>
      <h2>NPCs</h2>
      {actionError && <p role="alert">{actionError.message}</p>}
      {editing === 'new' ? (
        <>
          {form('New NPC', null)}
          <p className="hint">Save the NPC first, then edit it to add a portrait.</p>
        </>
      ) : (
        <button type="button" className="primary" onClick={() => open('new')}>
          Add NPC
        </button>
      )}
      {active.length === 0 && <p>No NPCs yet.</p>}
      {active.map((npc) =>
        editing === npc.id ? (
          <div key={npc.id}>
            {form(`Edit ${npc.name}`, npc.id, fieldsOf(npc))}
            <NpcPortraitEditor npc={npc} />
          </div>
        ) : (
          <NpcCard key={npc.id} npc={npc}>
            <button type="button" aria-label={`Edit ${npc.name}`} onClick={() => open(npc.id)}>
              Edit
            </button>{' '}
            <button
              type="button"
              aria-label={`Archive ${npc.name}`}
              disabled={busy}
              onClick={() => archive.mutate(npc.id)}
            >
              Archive
            </button>
          </NpcCard>
        ),
      )}
      {archived.length > 0 && (
        <section aria-labelledby={archivedHeadingId}>
          <h3 id={archivedHeadingId}>Archived</h3>
          {archived.map((npc) => (
            <NpcCard key={npc.id} npc={npc}>
              <button
                type="button"
                aria-label={`Restore ${npc.name}`}
                disabled={busy}
                onClick={() => restore.mutate(npc.id)}
              >
                Restore
              </button>
            </NpcCard>
          ))}
        </section>
      )}
    </section>
  )
}

function fieldsOf(npc: Npc): NpcFields {
  return {
    name: npc.name,
    description: npc.description,
    gmNotes: npc.gmNotes,
    gmOnly: npc.gmOnly,
  }
}

function NpcCard({ npc, children }: { npc: Npc; children: ReactNode }) {
  const headingId = useId()
  return (
    <article className="npc" aria-labelledby={headingId}>
      {npc.portraitVersion && (
        <img className="portrait" src={gmPortraitUrl(npc.id, npc.portraitVersion)} alt={`Portrait of ${npc.name}`} />
      )}
      <div className="npc-body">
        <div className="npc-heading">
          <h3 id={headingId}>{npc.name}</h3>
          {npc.gmOnly && <p className="seal">Hidden from players</p>}
        </div>
        {npc.description && <p className="npc-text">{npc.description}</p>}
        {npc.gmNotes && (
          <div className="secret">
            <p className="secret-title">GM notes</p>
            <p className="npc-text">{npc.gmNotes}</p>
          </div>
        )}
        <div className="npc-actions">{children}</div>
      </div>
    </article>
  )
}

export default NpcManager
