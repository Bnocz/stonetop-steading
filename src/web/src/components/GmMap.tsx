import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import {
  createSession,
  deleteSession,
  fetchMap,
  fetchSessions,
  gmMapImageUrl,
  saveRoute,
  sessionHeading,
  updateSession,
  uploadMapImage,
  type PlaySession,
  type Point,
  type SessionFields,
  type Waypoint,
} from '../api/map.ts'
import { mapStateAt, type MapState } from '../map/route.ts'
import MapView from './MapView.tsx'
import SessionNav from './SessionNav.tsx'
import StopList from './StopList.tsx'

type Mode = 'view' | 'route' | 'new' | 'edit'

const nothing: MapState = { start: null, waypoints: [], earlier: [], party: null }

function GmMap() {
  const queryClient = useQueryClient()
  const map = useQuery({ queryKey: ['map'], queryFn: fetchMap })
  const sessions = useQuery({ queryKey: ['sessions'], queryFn: fetchSessions })
  // null follows the latest session; otherwise the GM has picked one with the arrows.
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [mode, setMode] = useState<Mode>('view')
  const [draft, setDraft] = useState<Waypoint[]>([])
  const [moving, setMoving] = useState<number | null>(null)

  const refreshSessions = () => queryClient.invalidateQueries({ queryKey: ['sessions'] })
  const upload = useMutation({
    mutationFn: uploadMapImage,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['map'] }),
  })
  const saveSession = useMutation({
    mutationFn: ({ id, fields }: { id: string | null; fields: SessionFields }) =>
      id === null ? createSession(fields) : updateSession(id, fields),
    onSuccess: async (saved) => {
      setSelectedId(saved.id)
      setMode('view')
      await refreshSessions()
    },
  })
  const saveDraft = useMutation({
    mutationFn: ({ id, waypoints }: { id: string; waypoints: Waypoint[] }) => saveRoute(id, waypoints),
    onSuccess: async () => {
      setMode('view')
      await refreshSessions()
    },
  })
  const remove = useMutation({
    mutationFn: deleteSession,
    onSuccess: async () => {
      setSelectedId(null)
      await refreshSessions()
    },
  })

  if (map.isPending || sessions.isPending) return <p>Loading the map…</p>
  if (map.isError || sessions.isError) return <p>Could not load the map.</p>

  if (!map.data.image) {
    return (
      <section>
        <h2>Map</h2>
        <p>Upload your region map to start: a PNG, JPEG or WebP image up to 10 MB.</p>
        <MapUpload label="Map image" upload={upload} />
      </section>
    )
  }

  const list = sessions.data
  const found = list.findIndex((s) => s.id === selectedId)
  const index = found >= 0 ? found : list.length - 1
  const session: PlaySession | undefined = list[index]
  const state =
    session === undefined ? nothing : mapStateAt(list, index, mode === 'route' ? draft : undefined)

  function startRoute(current: PlaySession) {
    saveDraft.reset()
    setDraft(current.waypoints.map((w) => ({ ...w })))
    setMoving(null)
    setMode('route')
  }

  function placeStop(point: Point) {
    if (moving !== null) {
      setDraft(draft.map((stop, i) => (i === moving ? { ...stop, ...point } : stop)))
      setMoving(null)
    } else {
      setDraft([...draft, { ...point, label: '' }])
    }
  }

  function openForm(next: 'new' | 'edit') {
    saveSession.reset()
    setMode(next)
  }

  function confirmDelete(current: PlaySession) {
    if (window.confirm(`Delete ${sessionHeading(current)} and its route? This can't be undone.`)) {
      remove.mutate(current.id)
    }
  }

  const nextNumber = Math.min(999, (list[list.length - 1]?.number ?? 0) + 1)

  return (
    <section>
      <h2>Map</h2>
      {session ? (
        <SessionNav
          session={session}
          index={index}
          count={list.length}
          locked={mode !== 'view'}
          onSelect={(next) => setSelectedId(list[next].id)}
        >
          {session.gmOnly && <p className="seal">Hidden from players</p>}
        </SessionNav>
      ) : (
        <p>No sessions yet.</p>
      )}

      <MapView
        imageUrl={gmMapImageUrl(map.data.image.version)}
        state={state}
        onMapClick={mode === 'route' ? placeStop : undefined}
      />

      {remove.isError && <p role="alert">{remove.error.message}</p>}

      {mode === 'route' && session && (
        <section className="route-editor" aria-label="Route editor">
          <p>
            {moving === null
              ? 'Click the map to add the next stop. The route starts where the last session ended.'
              : `Click the map to move stop ${moving + 1}.`}
          </p>
          {saveDraft.isError && <p role="alert">{saveDraft.error.message}</p>}
          {draft.length > 0 && (
            <ol className="stop-list">
              {draft.map((stop, i) => (
                <li key={i}>
                  <input
                    aria-label={`Label for stop ${i + 1}`}
                    maxLength={100}
                    placeholder="Label (optional)"
                    value={stop.label}
                    onChange={(event) =>
                      setDraft(draft.map((s, j) => (j === i ? { ...s, label: event.target.value } : s)))
                    }
                  />{' '}
                  <button type="button" aria-label={`Move stop ${i + 1}`} onClick={() => setMoving(i)}>
                    Move
                  </button>{' '}
                  <button
                    type="button"
                    aria-label={`Remove stop ${i + 1}`}
                    onClick={() => {
                      setDraft(draft.filter((_, j) => j !== i))
                      setMoving(null)
                    }}
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ol>
          )}
          <div className="npc-actions">
            <button
              type="button"
              disabled={draft.length === 0}
              onClick={() => {
                setDraft(draft.slice(0, -1))
                setMoving(null)
              }}
            >
              Undo
            </button>
            <button
              type="button"
              className="primary"
              disabled={saveDraft.isPending}
              onClick={() => saveDraft.mutate({ id: session.id, waypoints: draft })}
            >
              Save route
            </button>
            <button type="button" onClick={() => setMode('view')}>
              Cancel
            </button>
          </div>
        </section>
      )}

      {(mode === 'new' || (mode === 'edit' && session)) && (
        <SessionForm
          heading={mode === 'new' ? 'New session' : `Edit ${sessionHeading(session!)}`}
          initial={
            mode === 'new'
              ? { number: nextNumber, title: '', gmOnly: true }
              : { number: session!.number, title: session!.title, gmOnly: session!.gmOnly }
          }
          saving={saveSession.isPending}
          error={saveSession.isError ? saveSession.error.message : null}
          onSave={(fields) => saveSession.mutate({ id: mode === 'new' ? null : session!.id, fields })}
          onCancel={() => setMode('view')}
        />
      )}

      {mode === 'view' && (
        <>
          {session && <StopList stops={session.waypoints} />}
          <div className="npc-actions">
            {session && (
              <>
                <button type="button" className="primary" onClick={() => startRoute(session)}>
                  Edit route
                </button>
                <button type="button" onClick={() => openForm('edit')}>
                  Edit session
                </button>
              </>
            )}
            <button type="button" onClick={() => openForm('new')}>
              New session
            </button>
            {session && (
              <button type="button" disabled={remove.isPending} onClick={() => confirmDelete(session)}>
                Delete session
              </button>
            )}
          </div>
          <MapUpload label="Replace map image" upload={upload} />
        </>
      )}
    </section>
  )
}

function MapUpload({
  label,
  upload,
}: {
  label: string
  upload: { mutate: (file: File) => void; isPending: boolean; isError: boolean; error: Error | null }
}) {
  const id = useId()
  return (
    <div className="map-upload">
      <label htmlFor={id}>{label}</label>{' '}
      <input
        id={id}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        disabled={upload.isPending}
        onChange={(event) => {
          const file = event.target.files?.[0]
          if (file) upload.mutate(file)
          event.target.value = ''
        }}
      />
      {upload.isPending && <p>Uploading…</p>}
      {upload.isError && upload.error && <p role="alert">{upload.error.message}</p>}
    </div>
  )
}

function SessionForm({
  heading,
  initial,
  saving,
  error,
  onSave,
  onCancel,
}: {
  heading: string
  initial: SessionFields
  saving: boolean
  error: string | null
  onSave: (fields: SessionFields) => void
  onCancel: () => void
}) {
  const id = useId()
  const [number, setNumber] = useState(String(initial.number))
  const [title, setTitle] = useState(initial.title)
  const [gmOnly, setGmOnly] = useState(initial.gmOnly)

  function submit(event: FormEvent) {
    event.preventDefault()
    onSave({ number: Number.parseInt(number, 10), title, gmOnly })
  }

  return (
    <form className="npc-form" aria-labelledby={`${id}-heading`} onSubmit={submit}>
      <h3 id={`${id}-heading`}>{heading}</h3>
      {error && <p role="alert">{error}</p>}
      <label htmlFor={`${id}-number`}>Number</label>
      <input
        id={`${id}-number`}
        type="number"
        min={1}
        max={999}
        value={number}
        onChange={(event) => setNumber(event.target.value)}
      />
      <label htmlFor={`${id}-title`}>Title</label>
      <input id={`${id}-title`} maxLength={100} value={title} onChange={(event) => setTitle(event.target.value)} />
      <label>
        <input type="checkbox" checked={gmOnly} onChange={(event) => setGmOnly(event.target.checked)} /> Hidden
        from players
      </label>
      <p>
        <button type="submit" className="primary" disabled={saving}>
          Save session
        </button>{' '}
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </p>
    </form>
  )
}

export default GmMap
