import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import PlayerNpcList from './PlayerNpcList.tsx'

const npcs = [
  { id: 'n1', name: 'Aratis', description: 'Smith.' },
  { id: 'n2', name: 'Vesna', description: '' },
]

function stubApi(portraits: Response) {
  const fetchMock = vi.fn((url: string) => {
    if (url === '/api/view/abc123/npcs') return Promise.resolve(Response.json(npcs))
    if (url === '/api/view/abc123/npc-portraits') return Promise.resolve(portraits)
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderList() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <PlayerNpcList token="abc123" />
    </QueryClientProvider>,
  )
}

test('shows portraits through the share link', async () => {
  stubApi(Response.json([{ npcId: 'n1', version: 'p1' }]))
  renderList()

  const aratis = within(await screen.findByRole('article', { name: 'Aratis' }))
  expect(await aratis.findByRole('img', { name: 'Portrait of Aratis' })).toHaveAttribute(
    'src',
    '/api/view/abc123/npcs/n1/portrait?v=p1',
  )
  expect(within(screen.getByRole('article', { name: 'Vesna' })).queryByRole('img')).not.toBeInTheDocument()
})

test('NPCs still show if the portraits cannot be loaded', async () => {
  stubApi(new Response(null, { status: 500 }))
  renderList()

  expect(await screen.findByRole('article', { name: 'Aratis' })).toBeInTheDocument()
  expect(screen.getByText('Smith.')).toBeInTheDocument()
  expect(screen.queryByRole('img')).not.toBeInTheDocument()
})

test('players get no portrait controls', async () => {
  const fetchMock = stubApi(Response.json([{ npcId: 'n1', version: 'p1' }]))
  renderList()

  await screen.findByRole('img', { name: 'Portrait of Aratis' })

  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  expect(screen.queryByLabelText('Portrait')).not.toBeInTheDocument()
  expect(fetchMock.mock.calls.map(([url]) => url).filter((url) => url.startsWith('/api/gm/'))).toEqual([])
})
