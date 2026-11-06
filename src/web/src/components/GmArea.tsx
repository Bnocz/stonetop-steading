import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchMe, logout } from '../api/auth.ts'
import ChangeLog from './ChangeLog.tsx'
import LoginForm from './LoginForm.tsx'
import SteadingSheet from './SteadingSheet.tsx'

function GmArea() {
  const queryClient = useQueryClient()
  const me = useQuery({ queryKey: ['me'], queryFn: fetchMe })
  const signOut = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(['me'], null)
      queryClient.removeQueries({ queryKey: ['steading'] })
      queryClient.removeQueries({ queryKey: ['steading-log'] })
    },
  })

  if (me.isPending) return null
  if (!me.data) return <LoginForm />

  return (
    <section>
      <p>Signed in as {me.data.username}</p>
      {signOut.isError && <p role="alert">{signOut.error.message}</p>}
      <button
        type="button"
        disabled={signOut.isPending}
        onClick={() => signOut.mutate()}
      >
        Sign out
      </button>
      <SteadingSheet />
      <ChangeLog />
    </section>
  )
}

export default GmArea
