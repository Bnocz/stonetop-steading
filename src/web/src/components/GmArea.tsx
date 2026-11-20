import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchMe, logout } from '../api/auth.ts'
import ChangeLog from './ChangeLog.tsx'
import LoginForm from './LoginForm.tsx'
import NpcManager from './NpcManager.tsx'
import ShareLink from './ShareLink.tsx'
import SteadingSheet from './SteadingSheet.tsx'
import Tabs from './Tabs.tsx'

function GmArea() {
  const queryClient = useQueryClient()
  const me = useQuery({ queryKey: ['me'], queryFn: fetchMe })
  const signOut = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(['me'], null)
      queryClient.removeQueries({ queryKey: ['steading'] })
      queryClient.removeQueries({ queryKey: ['steading-log'] })
      queryClient.removeQueries({ queryKey: ['share-link'] })
      queryClient.removeQueries({ queryKey: ['npcs'] })
    },
  })

  if (me.isPending) return null
  if (!me.data) return <LoginForm />

  return (
    <section>
      <div className="signed-in">
        <p>Signed in as {me.data.username}</p>
        <button
          type="button"
          disabled={signOut.isPending}
          onClick={() => signOut.mutate()}
        >
          Sign out
        </button>
      </div>
      {signOut.isError && <p role="alert">{signOut.error.message}</p>}
      <Tabs
        label="GM sections"
        tabs={[
          {
            label: 'Steading',
            content: (
              <>
                <SteadingSheet />
                <ShareLink />
                <ChangeLog />
              </>
            ),
          },
          { label: 'NPCs', content: <NpcManager /> },
        ]}
      />
    </section>
  )
}

export default GmArea
