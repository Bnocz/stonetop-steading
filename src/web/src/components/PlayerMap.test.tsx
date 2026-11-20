import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import PlayerMap from './PlayerMap.tsx'

const sessions = [
  {
    id: 's1',
    number: 1,
    title: 'Setting Out',
    waypoints: [
      { x: 0.69, y: 0.49, label: 'Stonetop' },
      { x: 0.21, y: 0.24, label: 'The Foothills' },
    ],
  },
  { id: 's2', number: 2, title: 'Home Again', waypoints: [{ x: 0.69, y: 0.49, label: 'Stonetop' }] },
]

function stubApi(options: { image?: boolean; list?: object[]; status?: number } = {}) {
  const fetchMock = vi.fn((url: string) => {
    if (options.status) return Promise.resolve(new Response(null, { status: options.status }))
    if (url === '/api/view/abc123/map') {
      return Promise.resolve(
        Response.json({ image: (options.image ?? true) ? { version: 'v1', contentType: 'image/png' } : null }),
      )
    }
    if (url === '/api/view/abc123/sessions') return Promise.resolve(Response.json(options.list ?? sessions))
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderMap() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <PlayerMap token="abc123" />
    </QueryClientProvider>,
  )
}

test('opens on the latest session and the arrows step back to the first', async () => {
  stubApi()
  renderMap()

  expect(await screen.findByRole('heading', { name: 'Session 2: Home Again' })).toBeInTheDocument()
  expect(screen.getByText('2 of 2')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Next session' })).toBeDisabled()

  fireEvent.click(screen.getByRole('button', { name: 'Previous session' }))

  expect(screen.getByRole('heading', { name: 'Session 1: Setting Out' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Previous session' })).toBeDisabled()
})

test('shows the route stops and where the party is', async () => {
  stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'Previous session' }))

  const stops = screen.getByRole('list', { name: 'Stops' })
  expect(within(stops).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
    'Stonetop',
    'The Foothills (the party is here)',
  ])
  expect(screen.getByLabelText('Stop 2: The Foothills')).toBeInTheDocument()
  expect(screen.getByLabelText('The party is here')).toBeInTheDocument()
})

test('uses the share link to load the map image', async () => {
  stubApi()
  renderMap()

  await screen.findByRole('heading', { name: 'Session 2: Home Again' })

  expect(document.querySelector('img')).toHaveAttribute('src', '/api/view/abc123/map/image?v=v1')
})

test('is read-only and never calls the GM API', async () => {
  const fetchMock = stubApi()
  renderMap()

  await screen.findByRole('heading', { name: 'Session 2: Home Again' })
  fireEvent.click(screen.getByLabelText('Region map'))

  expect(screen.getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual([
    'Previous session',
    'Next session',
  ])
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  expect(screen.queryByText('Hidden from players')).not.toBeInTheDocument()
  expect(fetchMock.mock.calls.map(([url]) => url).filter((url) => url.startsWith('/api/gm/'))).toEqual([])
})

test('says when the GM has not added a map', async () => {
  stubApi({ image: false })
  renderMap()

  expect(await screen.findByText("The GM hasn't added a map yet.")).toBeInTheDocument()
  expect(screen.queryByLabelText('Region map')).not.toBeInTheDocument()
})

test('shows the bare map when no sessions are shared yet', async () => {
  stubApi({ list: [] })
  renderMap()

  expect(await screen.findByText('No sessions yet.')).toBeInTheDocument()
  expect(screen.getByLabelText('Region map')).toBeInTheDocument()
  expect(screen.queryByLabelText('The party is here')).not.toBeInTheDocument()
})

test('a load error shows a message', async () => {
  stubApi({ status: 500 })
  renderMap()

  expect(await screen.findByText('Could not load the map.')).toBeInTheDocument()
})
