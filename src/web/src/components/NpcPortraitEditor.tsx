import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useId, useState } from 'react'
import { gmPortraitUrl, removePortrait, uploadPortrait, type Npc } from '../api/npcs.ts'
import { shrinkImage } from '../images/shrinkImage.ts'

// Shown under the edit form of an existing NPC. Changes save straight away, apart from the form.
function NpcPortraitEditor({ npc }: { npc: Npc }) {
  const queryClient = useQueryClient()
  const id = useId()
  const [readError, setReadError] = useState<string | null>(null)
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['npcs'] })
  const upload = useMutation({
    mutationFn: async (file: File) => {
      let small: Blob
      try {
        small = await shrinkImage(file)
      } catch {
        throw new Error('Could not read that picture. Try a PNG, JPEG or WebP file.')
      }
      return uploadPortrait(npc.id, small)
    },
    onSuccess: refresh,
  })
  const remove = useMutation({ mutationFn: () => removePortrait(npc.id), onSuccess: refresh })

  const error = readError ?? (upload.isError ? upload.error.message : null) ?? (remove.isError ? remove.error.message : null)
  const busy = upload.isPending || remove.isPending

  function confirmRemove() {
    if (window.confirm(`Remove ${npc.name}'s portrait?`)) remove.mutate()
  }

  return (
    <div className="portrait-editor">
      {npc.portraitVersion && (
        <img className="portrait" src={gmPortraitUrl(npc.id, npc.portraitVersion)} alt={`Portrait of ${npc.name}`} />
      )}
      <div>
        <label htmlFor={id}>Portrait</label>{' '}
        <input
          id={id}
          type="file"
          accept="image/png,image/jpeg,image/webp"
          disabled={busy}
          onChange={(event) => {
            const file = event.target.files?.[0]
            setReadError(null)
            if (file) upload.mutate(file)
            event.target.value = ''
          }}
        />
        {upload.isPending && <p>Uploading…</p>}
        {error && <p role="alert">{error}</p>}
        {npc.portraitVersion && (
          <p>
            <button type="button" disabled={busy} onClick={confirmRemove}>
              Remove portrait
            </button>
          </p>
        )}
      </div>
    </div>
  )
}

export default NpcPortraitEditor
