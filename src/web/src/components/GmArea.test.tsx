import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import GmArea from './GmArea.tsx'

function stubApi(options: { signedIn: boolean; loginStatus?: number }) {
  let signedIn = options.signedIn
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url === '/api/gm/login' && init?.method === 'POST') {
      const status = options.loginStatus ?? 200
      if (status !== 200) return Promise.resolve(new Response(null, { status }))
      signedIn = true
      return Promise.resolve(Response.json({ username: 'gm' }))
    }
    if (url === '/api/gm/logout' && init?.method === 'POST') {
      signedIn = false
      return Promise.resolve(new Response(null, { status: 204 }))
    }
    if (url === '/api/gm/me') {
      return Promise.resolve(
        signedIn
          ? Response.json({ username: 'gm' })
          : new Response(null, { status: 401 }),
      )
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderArea() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <GmArea />
    </QueryClientProvider>,
  )
}

function submitLogin(username: string, password: string) {
  fireEvent.change(screen.getByLabelText('Username'), {
    target: { value: username },
  })
  fireEvent.change(screen.getByLabelText('Password'), {
    target: { value: password },
  })
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

test('shows the login form when signed out', async () => {
  stubApi({ signedIn: false })

  renderArea()

  expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
})

test('signing in sends the credentials and shows the signed-in state', async () => {
  const fetchMock = stubApi({ signedIn: false })
  renderArea()
  await screen.findByRole('button', { name: 'Sign in' })

  submitLogin('gm', 'secret')

  expect(await screen.findByText('Signed in as gm')).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/gm/login',
    expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ username: 'gm', password: 'secret' }),
    }),
  )
})

test('wrong credentials show an error and keep the form', async () => {
  stubApi({ signedIn: false, loginStatus: 401 })
  renderArea()
  await screen.findByRole('button', { name: 'Sign in' })

  submitLogin('gm', 'wrong')

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Wrong username or password.',
  )
  expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
})

test('too many attempts show a wait message', async () => {
  stubApi({ signedIn: false, loginStatus: 429 })
  renderArea()
  await screen.findByRole('button', { name: 'Sign in' })

  submitLogin('gm', 'wrong')

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Too many attempts. Wait a minute and try again.',
  )
})

test('an existing session skips the login form', async () => {
  stubApi({ signedIn: true })

  renderArea()

  expect(await screen.findByText('Signed in as gm')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Sign in' })).not.toBeInTheDocument()
})

test('signing out returns to the login form', async () => {
  stubApi({ signedIn: true })
  renderArea()

  fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

  expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
})

test('a response without a username counts as signed out', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(Response.json({ status: 'ok' }))),
  )

  renderArea()

  expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
})
