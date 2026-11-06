import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import ShareLink from './ShareLink.tsx'

afterEach(() => {
  vi.restoreAllMocks()
})

function stubApi() {
  let token = 'first-token'
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url === '/api/gm/steading/share-link') {
      if (init?.method === 'POST') token = 'second-token'
      return Promise.resolve(Response.json({ token }))
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderLink() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <ShareLink />
    </QueryClientProvider>,
  )
}

const linkFor = (token: string) => `${window.location.origin}/view/${token}`

test('shows the full player link', async () => {
  stubApi()

  renderLink()

  expect(await screen.findByLabelText('Player link')).toHaveValue(linkFor('first-token'))
})

test('copies the link', async () => {
  stubApi()
  const writeText = vi.fn(() => Promise.resolve())
  vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } })
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Copy' }))

  expect(await screen.findByText('Copied')).toBeInTheDocument()
  expect(writeText).toHaveBeenCalledWith(linkFor('first-token'))
})

test('replacing asks first and does nothing if declined', async () => {
  const fetchMock = stubApi()
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Replace link' }))

  await waitFor(() => expect(confirm).toHaveBeenCalled())
  expect(fetchMock).not.toHaveBeenCalledWith(
    '/api/gm/steading/share-link',
    expect.objectContaining({ method: 'POST' }),
  )
  expect(screen.getByLabelText('Player link')).toHaveValue(linkFor('first-token'))
})

test('replacing after confirming shows the new link', async () => {
  const fetchMock = stubApi()
  vi.spyOn(window, 'confirm').mockReturnValue(true)
  renderLink()

  fireEvent.click(await screen.findByRole('button', { name: 'Replace link' }))

  await waitFor(() =>
    expect(screen.getByLabelText('Player link')).toHaveValue(linkFor('second-token')),
  )
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/gm/steading/share-link',
    expect.objectContaining({ method: 'POST' }),
  )
})

test('a failed load shows an error', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(new Response(null, { status: 500 }))),
  )

  renderLink()

  expect(await screen.findByText('Could not load the player link.')).toBeInTheDocument()
})
