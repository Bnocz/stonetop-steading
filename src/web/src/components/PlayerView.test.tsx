import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import PlayerView from './PlayerView.tsx'

const sheet = {
  name: 'Stonetop',
  stats: [
    { stat: 'Fortunes', value: 1, label: null },
    { stat: 'Surplus', value: 2, label: null },
    { stat: 'Prosperity', value: -2, label: 'Dirt' },
  ],
  debilities: ['Lacking'],
}

const log = [
  {
    stat: 'Fortunes',
    oldValue: 0,
    newValue: 1,
    changedAt: '2026-11-06T12:00:00Z',
  },
]

function stubApi() {
  const fetchMock = vi.fn((url: string) => {
    if (url === '/api/view/abc123/steading') {
      return Promise.resolve(Response.json(sheet))
    }
    if (url === '/api/view/abc123/log') return Promise.resolve(Response.json(log))
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderView(token = 'abc123') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <PlayerView token={token} />
    </QueryClientProvider>,
  )
}

test('shows effective values, labels, debilities and the log', async () => {
  stubApi()

  renderView()

  const prosperity = within(await screen.findByRole('row', { name: /^Prosperity\b/ }))
  expect(prosperity.getByRole('cell', { name: '-2' })).toBeInTheDocument()
  expect(prosperity.getByRole('cell', { name: 'Dirt' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Stonetop' })).toBeInTheDocument()
  expect(screen.getByText('Debilities: Lacking')).toBeInTheDocument()
  expect(await screen.findByText(/Fortunes: \+0 → \+1/)).toBeInTheDocument()
})

test('modifiers show a sign and surplus does not', async () => {
  stubApi()

  renderView()

  const fortunes = within(await screen.findByRole('row', { name: /^Fortunes\b/ }))
  const surplus = within(await screen.findByRole('row', { name: /^Surplus\b/ }))
  expect(fortunes.getByRole('cell', { name: '+1' })).toBeInTheDocument()
  expect(surplus.getByRole('cell', { name: '2' })).toBeInTheDocument()
})

test('is read-only and never calls the GM API', async () => {
  const fetchMock = stubApi()

  renderView()

  await screen.findByRole('row', { name: /^Prosperity\b/ })
  await screen.findByText(/Fortunes: \+0 → \+1/)
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  const urls = fetchMock.mock.calls.map(([url]) => url)
  expect(urls.length).toBeGreaterThan(0)
  expect(urls.filter((url) => url.startsWith('/api/gm/'))).toEqual([])
})

test('an unknown link shows the invalid link message', async () => {
  stubApi()

  renderView('wrong')

  expect(
    await screen.findByText("This link isn't valid. Ask your GM for a new one."),
  ).toBeInTheDocument()
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
})

test('a server error shows a load error, not the invalid link message', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(new Response(null, { status: 500 }))),
  )

  renderView()

  expect(await screen.findByText('Could not load the steading.')).toBeInTheDocument()
})

const playerNpcs = [
  { id: 'n1', name: 'Aratis', description: 'Smith by the well.' },
  { id: 'n2', name: 'Vesna', description: '' },
]

function stubApiWithNpcs(npcs: object[] = playerNpcs) {
  const fetchMock = vi.fn((url: string) => {
    if (url === '/api/view/abc123/steading') return Promise.resolve(Response.json(sheet))
    if (url === '/api/view/abc123/log') return Promise.resolve(Response.json(log))
    if (url === '/api/view/abc123/npcs') return Promise.resolve(Response.json(npcs))
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

test('the steading tab is selected first and NPCs load only when their tab opens', async () => {
  const fetchMock = stubApiWithNpcs()

  renderView()

  await screen.findByRole('row', { name: /^Prosperity\b/ })
  expect(screen.getByRole('tab', { name: 'Steading' })).toHaveAttribute('aria-selected', 'true')
  expect(screen.getByRole('tab', { name: 'NPCs' })).toHaveAttribute('aria-selected', 'false')
  expect(fetchMock.mock.calls.map(([url]) => url)).not.toContain('/api/view/abc123/npcs')
})

test('the NPCs tab lists NPCs with their descriptions', async () => {
  stubApiWithNpcs()
  renderView()

  fireEvent.click(await screen.findByRole('tab', { name: 'NPCs' }))

  const aratis = within(await screen.findByRole('article', { name: 'Aratis' }))
  expect(aratis.getByText('Smith by the well.')).toBeInTheDocument()
  expect(screen.getByRole('article', { name: 'Vesna' })).toBeInTheDocument()
  expect(screen.getByRole('tab', { name: 'NPCs' })).toHaveAttribute('aria-selected', 'true')
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
})

test('the NPCs tab says when there are none', async () => {
  stubApiWithNpcs([])
  renderView()

  fireEvent.click(await screen.findByRole('tab', { name: 'NPCs' }))

  expect(await screen.findByText('No NPCs yet.')).toBeInTheDocument()
})

test('an NPC load error shows a message', async () => {
  stubApi()
  renderView()

  fireEvent.click(await screen.findByRole('tab', { name: 'NPCs' }))

  expect(await screen.findByText('Could not load the NPCs.')).toBeInTheDocument()
})
