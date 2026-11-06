import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { fetchShareLink, replaceShareLink } from '../api/steading.ts'

function ShareLink() {
  const queryClient = useQueryClient()
  const [copyStatus, setCopyStatus] = useState('')
  const shareLink = useQuery({ queryKey: ['share-link'], queryFn: fetchShareLink })
  const replace = useMutation({
    mutationFn: replaceShareLink,
    onSuccess: (updated) => {
      queryClient.setQueryData(['share-link'], updated)
      setCopyStatus('')
    },
  })

  if (shareLink.isPending) return null
  if (shareLink.isError) return <p>Could not load the player link.</p>

  const link = `${window.location.origin}/view/${shareLink.data.token}`

  async function copy() {
    try {
      await navigator.clipboard.writeText(link)
      setCopyStatus('Copied')
    } catch {
      setCopyStatus('Could not copy. Select the link and copy it yourself.')
    }
  }

  function confirmReplace() {
    const confirmed = window.confirm(
      'Replace the player link? The current link will stop working, and players will need the new one.',
    )
    if (confirmed) replace.mutate()
  }

  return (
    <section>
      <h2>Player link</h2>
      <p>Anyone with this link can read the sheet and change log.</p>
      <p>
        <input
          aria-label="Player link"
          readOnly
          size={40}
          value={link}
          onFocus={(event) => event.target.select()}
        />
      </p>
      {replace.isError && <p role="alert">{replace.error.message}</p>}
      <button type="button" onClick={() => void copy()}>
        Copy
      </button>{' '}
      <button type="button" disabled={replace.isPending} onClick={confirmReplace}>
        Replace link
      </button>{' '}
      <span>{copyStatus}</span>
    </section>
  )
}

export default ShareLink
