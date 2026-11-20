import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import ShareLink from './ShareLink.tsx'

afterEach(() => {
  vi.restoreAllMocks()
})

function stubApi(options: { fail?: boolean } = {}) {
  let token = 'first-token'
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url === '/api/gm/steading/share-link/readable' && init?.method === 'POST') {
      if (options.fail) {
        return Promise.resolve(Response.json({ detail: 'Try again.' }, { status: 409 }))
      }
      token = 'ember-moss-lantern-crow'
      return Promise.resolve(Response.json({ token }))
    }
    if (url === '/api/gm/steading/share-link') return Promise.resolve(Response.json({ token }))
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderLink() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <ShareLink />
    </QueryClientProvider>,
  )
}

const linkFor = (token: string) => `${window.location.origin}/view/${token}`

test('making a readable link asks first and shows the new link', async () => {
  const fetchMock = stubApi()
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Make a readable link' }))

  await waitFor(() =>
    expect(screen.getByLabelText('Player link')).toHaveValue(linkFor('ember-moss-lantern-crow')),
  )
  expect(confirm).toHaveBeenCalled()
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/gm/steading/share-link/readable',
    expect.objectContaining({ method: 'POST' }),
  )
})

test('declining keeps the current link', async () => {
  const fetchMock = stubApi()
  vi.spyOn(window, 'confirm').mockReturnValue(false)
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Make a readable link' }))

  expect(screen.getByLabelText('Player link')).toHaveValue(linkFor('first-token'))
  expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toEqual([])
})

test('a failure shows the server message', async () => {
  stubApi({ fail: true })
  vi.spyOn(window, 'confirm').mockReturnValue(true)
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Make a readable link' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('Try again.')
  expect(screen.getByLabelText('Player link')).toHaveValue(linkFor('first-token'))
})
