export interface GmUser {
  username: string
}

function isGmUser(body: unknown): body is GmUser {
  return (
    typeof body === 'object' &&
    body !== null &&
    'username' in body &&
    typeof body.username === 'string'
  )
}

// Returns null when signed out, so the app can show the login form.
export async function fetchMe(): Promise<GmUser | null> {
  const response = await fetch('/api/gm/me')
  if (!response.ok) return null
  const body: unknown = await response.json()
  return isGmUser(body) ? body : null
}

export async function login(username: string, password: string): Promise<GmUser> {
  const response = await fetch('/api/gm/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  })
  if (response.status === 401) throw new Error('Wrong username or password.')
  if (response.status === 429) {
    throw new Error('Too many attempts. Wait a minute and try again.')
  }
  if (!response.ok) throw new Error('Sign-in failed. Try again.')
  return (await response.json()) as GmUser
}

export async function logout(): Promise<void> {
  const response = await fetch('/api/gm/logout', { method: 'POST' })
  if (!response.ok) throw new Error('Sign-out failed. Try again.')
}
