import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { login } from '../api/auth.ts'

function LoginForm() {
  const queryClient = useQueryClient()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const signIn = useMutation({
    mutationFn: () => login(username, password),
    onSuccess: (user) => queryClient.setQueryData(['me'], user),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    signIn.mutate()
  }

  return (
    <form onSubmit={handleSubmit}>
      <h2>GM sign in</h2>
      <p>
        <label htmlFor="gm-username">Username</label>
        <br />
        <input
          id="gm-username"
          autoComplete="username"
          required
          value={username}
          onChange={(event) => setUsername(event.target.value)}
        />
      </p>
      <p>
        <label htmlFor="gm-password">Password</label>
        <br />
        <input
          id="gm-password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
      </p>
      {signIn.isError && <p role="alert">{signIn.error.message}</p>}
      <button type="submit" disabled={signIn.isPending}>
        Sign in
      </button>
    </form>
  )
}

export default LoginForm
