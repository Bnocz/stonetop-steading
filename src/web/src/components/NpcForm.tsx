import { useId, useState, type FormEvent } from 'react'
import type { NpcFields } from '../api/npcs.ts'

const emptyFields: NpcFields = { name: '', description: '', gmNotes: '', gmOnly: false }

function NpcForm({
  heading,
  initial = emptyFields,
  saving,
  error,
  onSave,
  onCancel,
}: {
  heading: string
  initial?: NpcFields
  saving: boolean
  error: string | null
  onSave: (fields: NpcFields) => void
  onCancel: () => void
}) {
  const [fields, setFields] = useState(initial)
  const id = useId()

  function submit(event: FormEvent) {
    event.preventDefault()
    onSave(fields)
  }

  return (
    <form className="npc-form" aria-labelledby={`${id}-heading`} onSubmit={submit}>
      <h3 id={`${id}-heading`}>{heading}</h3>
      {error && <p role="alert">{error}</p>}
      <label htmlFor={`${id}-name`}>Name</label>
      <input
        id={`${id}-name`}
        maxLength={100}
        value={fields.name}
        onChange={(event) => setFields({ ...fields, name: event.target.value })}
      />
      <label htmlFor={`${id}-description`}>Description</label>
      <textarea
        id={`${id}-description`}
        rows={3}
        maxLength={2000}
        value={fields.description}
        onChange={(event) => setFields({ ...fields, description: event.target.value })}
      />
      <label htmlFor={`${id}-notes`}>GM notes</label>
      <textarea
        id={`${id}-notes`}
        rows={3}
        maxLength={4000}
        value={fields.gmNotes}
        onChange={(event) => setFields({ ...fields, gmNotes: event.target.value })}
      />
      <label>
        <input
          type="checkbox"
          checked={fields.gmOnly}
          onChange={(event) => setFields({ ...fields, gmOnly: event.target.checked })}
        />{' '}
        Hidden from players
      </label>
      <p>
        <button type="submit" disabled={saving}>
          Save
        </button>{' '}
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </p>
    </form>
  )
}

export default NpcForm
