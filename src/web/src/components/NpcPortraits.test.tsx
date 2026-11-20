import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { shrinkImage } from '../images/shrinkImage.ts'
import NpcManager from './NpcManager.tsx'

vi.mock('../images/shrinkImage.ts', () => ({ shrinkImage: vi.fn() }))

const shrunk = new Blob(['small'], { type: 'image/webp' })

beforeEach(() => {
  vi.mocked(shrinkImage).mockResolvedValue(shrunk)
})

afterEach(() => {
  vi.restoreAllMocks()
})

interface FakeNpc {
  id: string
  name: string
  description: string
  gmNotes: string
  gmOnly: boolean
  archived: boolean
  portraitVersion: string | null
}

function stubApi(options: { uploadError?: string } = {}) {
  const npcs: FakeNpc[] = [
    { id: 'n1', name: 'Aratis', description: '', gmNotes: '', gmOnly: false, archived: false, portraitVersion: 'p1' },
    { id: 'n2', name: 'Vesna', description: '', gmNotes: '', gmOnly: false, archived: false, portraitVersion: null },
  ]
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    if (url === '/api/gm/npcs' && method === 'GET') return Promise.resolve(Response.json(npcs))
    const match = /^\/api\/gm\/npcs\/([^/]+)\/portrait$/.exec(url)
    const npc = npcs.find((n) => n.id === match?.[1])
    if (npc && method === 'PUT') {
      if (options.uploadError) {
        return Promise.resolve(Response.json({ detail: options.uploadError }, { status: 400 }))
      }
      npc.portraitVersion = 'p2'
      return Promise.resolve(Response.json({ version: 'p2' }))
    }
    if (npc && method === 'DELETE') {
      npc.portraitVersion = null
      return Promise.resolve(new Response(null, { status: 204 }))
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderManager() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <NpcManager />
    </QueryClientProvider>,
  )
}

function choosePortrait(file = new File(['big-photo'], 'aratis.jpg', { type: 'image/jpeg' })) {
  fireEvent.change(screen.getByLabelText('Portrait'), { target: { files: [file] } })
  return file
}

test('cards show the portrait when an NPC has one', async () => {
  stubApi()
  renderManager()

  const aratis = within(await screen.findByRole('article', { name: 'Aratis' }))
  expect(aratis.getByRole('img', { name: 'Portrait of Aratis' })).toHaveAttribute(
    'src',
    '/api/gm/npcs/n1/portrait?v=p1',
  )
  const vesna = within(screen.getByRole('article', { name: 'Vesna' }))
  expect(vesna.queryByRole('img')).not.toBeInTheDocument()
})

test('choosing a picture shrinks it, uploads it and shows the new portrait', async () => {
  const fetchMock = stubApi()
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Aratis' }))

  const file = choosePortrait()

  await waitFor(() =>
    expect(screen.getByRole('img', { name: 'Portrait of Aratis' })).toHaveAttribute(
      'src',
      '/api/gm/npcs/n1/portrait?v=p2',
    ),
  )
  expect(shrinkImage).toHaveBeenCalledWith(file)
  const [[url, init]] = fetchMock.mock.calls.filter(([, i]) => i?.method === 'PUT')
  expect(url).toBe('/api/gm/npcs/n1/portrait')
  expect(init!.body).toBe(shrunk)
  expect(init!.headers).toEqual({ 'Content-Type': 'image/webp' })
})

test('removing a portrait asks first', async () => {
  const fetchMock = stubApi()
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Aratis' }))

  fireEvent.click(screen.getByRole('button', { name: 'Remove portrait' }))

  await waitFor(() => expect(screen.queryByRole('img', { name: 'Portrait of Aratis' })).not.toBeInTheDocument())
  expect(confirm).toHaveBeenCalled()
  expect(fetchMock.mock.calls.filter(([, i]) => i?.method === 'DELETE').map(([u]) => u)).toEqual([
    '/api/gm/npcs/n1/portrait',
  ])
  expect(screen.queryByRole('button', { name: 'Remove portrait' })).not.toBeInTheDocument()
})

test('declining the removal keeps the portrait', async () => {
  const fetchMock = stubApi()
  vi.spyOn(window, 'confirm').mockReturnValue(false)
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Aratis' }))

  fireEvent.click(screen.getByRole('button', { name: 'Remove portrait' }))

  expect(fetchMock.mock.calls.filter(([, i]) => i?.method === 'DELETE')).toEqual([])
  expect(screen.getByRole('img', { name: 'Portrait of Aratis' })).toBeInTheDocument()
})

test('a rejected upload shows the server message', async () => {
  stubApi({ uploadError: 'The portrait must be a PNG, JPEG or WebP image.' })
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Vesna' }))

  choosePortrait()

  expect(await screen.findByRole('alert')).toHaveTextContent('The portrait must be a PNG, JPEG or WebP image.')
})

test('a picture the browser cannot read shows a message and uploads nothing', async () => {
  const fetchMock = stubApi()
  vi.mocked(shrinkImage).mockRejectedValue(new Error('decode failed'))
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Vesna' }))

  choosePortrait()

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Could not read that picture. Try a PNG, JPEG or WebP file.',
  )
  expect(fetchMock.mock.calls.filter(([, i]) => i?.method === 'PUT')).toEqual([])
})

test('a new NPC is saved before it can have a portrait', async () => {
  stubApi()
  renderManager()

  fireEvent.click(await screen.findByRole('button', { name: 'Add NPC' }))

  expect(screen.queryByLabelText('Portrait')).not.toBeInTheDocument()
  expect(screen.getByText('Save the NPC first, then edit it to add a portrait.')).toBeInTheDocument()
})
