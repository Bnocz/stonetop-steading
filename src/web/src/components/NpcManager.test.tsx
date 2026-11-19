import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import NpcManager from './NpcManager.tsx'

interface FakeNpc {
  id: string
  name: string
  description: string
  gmNotes: string
  gmOnly: boolean
  archived: boolean
}

function seedNpcs(): FakeNpc[] {
  return [
    {
      id: 'n1',
      name: 'Aratis',
      description: 'Smith by the well.',
      gmNotes: 'Owes the Hillfolk a debt.',
      gmOnly: false,
      archived: false,
    },
    {
      id: 'n2',
      name: 'Hidden Stranger',
      description: '',
      gmNotes: '',
      gmOnly: true,
      archived: false,
    },
    {
      id: 'n3',
      name: 'Old Tomas',
      description: 'Left for Marshedge.',
      gmNotes: '',
      gmOnly: false,
      archived: true,
    },
  ]
}

function stubApi(options: { npcs?: FakeNpc[]; saveError?: string } = {}) {
  const npcs = options.npcs ?? seedNpcs()
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    if (url === '/api/gm/npcs' && method === 'GET') {
      return Promise.resolve(Response.json(npcs))
    }
    if (options.saveError && method !== 'GET') {
      return Promise.resolve(
        Response.json({ detail: options.saveError }, { status: 400 }),
      )
    }
    if (url === '/api/gm/npcs' && method === 'POST') {
      const body = JSON.parse(init!.body as string) as Omit<FakeNpc, 'id' | 'archived'>
      const created = { ...body, id: `n${npcs.length + 1}`, archived: false }
      npcs.push(created)
      return Promise.resolve(Response.json(created, { status: 201 }))
    }
    const match = /^\/api\/gm\/npcs\/([^/]+)(\/restore)?$/.exec(url)
    const npc = npcs.find((n) => n.id === match?.[1])
    if (npc && method === 'PUT') {
      Object.assign(npc, JSON.parse(init!.body as string))
      return Promise.resolve(Response.json(npc))
    }
    if (npc && method === 'DELETE') {
      npc.archived = true
      return Promise.resolve(Response.json(npc))
    }
    if (npc && method === 'POST' && match?.[2]) {
      npc.archived = false
      return Promise.resolve(Response.json(npc))
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderManager() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <NpcManager />
    </QueryClientProvider>,
  )
}

function fillForm(fields: {
  name?: string
  description?: string
  gmNotes?: string
}) {
  if (fields.name !== undefined) {
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: fields.name } })
  }
  if (fields.description !== undefined) {
    fireEvent.change(screen.getByLabelText('Description'), {
      target: { value: fields.description },
    })
  }
  if (fields.gmNotes !== undefined) {
    fireEvent.change(screen.getByLabelText('GM notes'), {
      target: { value: fields.gmNotes },
    })
  }
}

function callsWith(fetchMock: ReturnType<typeof stubApi>, method: string) {
  return fetchMock.mock.calls.filter(([, init]) => init?.method === method)
}

test('lists NPCs with descriptions, GM notes and a hidden marker', async () => {
  stubApi()

  renderManager()

  const aratis = within(await screen.findByRole('article', { name: 'Aratis' }))
  expect(aratis.getByText('Smith by the well.')).toBeInTheDocument()
  expect(aratis.getByText(/Owes the Hillfolk a debt\./)).toBeInTheDocument()
  expect(aratis.queryByText('Hidden from players')).not.toBeInTheDocument()
  const stranger = within(screen.getByRole('article', { name: 'Hidden Stranger' }))
  expect(stranger.getByText('Hidden from players')).toBeInTheDocument()
})

test('archived NPCs are listed separately with a restore button', async () => {
  stubApi()

  renderManager()

  const archived = within(await screen.findByRole('region', { name: 'Archived' }))
  expect(archived.getByRole('article', { name: 'Old Tomas' })).toBeInTheDocument()
  expect(archived.getByRole('button', { name: 'Restore Old Tomas' })).toBeInTheDocument()
  expect(archived.queryByRole('article', { name: 'Aratis' })).not.toBeInTheDocument()
})

test('shows an empty message when there are no NPCs', async () => {
  stubApi({ npcs: [] })

  renderManager()

  expect(await screen.findByText('No NPCs yet.')).toBeInTheDocument()
  expect(screen.queryByRole('region', { name: 'Archived' })).not.toBeInTheDocument()
})

test('adding an NPC sends every field and shows it in the list', async () => {
  const fetchMock = stubApi({ npcs: [] })
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Add NPC' }))

  fillForm({ name: 'Vesna', description: 'Runs the Pavilion.', gmNotes: 'A spy.' })
  fireEvent.click(screen.getByLabelText('Hidden from players'))
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))

  const vesna = within(await screen.findByRole('article', { name: 'Vesna' }))
  expect(vesna.getByText('Runs the Pavilion.')).toBeInTheDocument()
  const [[url, init]] = callsWith(fetchMock, 'POST')
  expect(url).toBe('/api/gm/npcs')
  expect(JSON.parse(init!.body as string)).toEqual({
    name: 'Vesna',
    description: 'Runs the Pavilion.',
    gmNotes: 'A spy.',
    gmOnly: true,
  })
  expect(screen.queryByLabelText('Name')).not.toBeInTheDocument()
})

test('cancel closes the form without saving', async () => {
  const fetchMock = stubApi()
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Add NPC' }))

  fillForm({ name: 'Nobody' })
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

  expect(screen.queryByLabelText('Name')).not.toBeInTheDocument()
  expect(callsWith(fetchMock, 'POST')).toEqual([])
})

test('editing starts from the current values and sends a PUT', async () => {
  const fetchMock = stubApi()
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Aratis' }))

  expect(screen.getByLabelText('Name')).toHaveValue('Aratis')
  expect(screen.getByLabelText('Description')).toHaveValue('Smith by the well.')
  expect(screen.getByLabelText('GM notes')).toHaveValue('Owes the Hillfolk a debt.')
  expect(screen.getByLabelText('Hidden from players')).not.toBeChecked()
  fillForm({ description: 'Moved to the mill.' })
  fireEvent.click(screen.getByRole('button', { name: 'Save' }))

  const aratis = within(await screen.findByRole('article', { name: 'Aratis' }))
  expect(await aratis.findByText('Moved to the mill.')).toBeInTheDocument()
  const [[url, init]] = callsWith(fetchMock, 'PUT')
  expect(url).toBe('/api/gm/npcs/n1')
  expect(JSON.parse(init!.body as string)).toEqual({
    name: 'Aratis',
    description: 'Moved to the mill.',
    gmNotes: 'Owes the Hillfolk a debt.',
    gmOnly: false,
  })
})

test('archiving moves the NPC to the archived list and restore brings it back', async () => {
  const fetchMock = stubApi()
  renderManager()

  fireEvent.click(await screen.findByRole('button', { name: 'Archive Aratis' }))

  const archived = within(await screen.findByRole('region', { name: 'Archived' }))
  expect(await archived.findByRole('article', { name: 'Aratis' })).toBeInTheDocument()
  expect(callsWith(fetchMock, 'DELETE').map(([url]) => url)).toEqual(['/api/gm/npcs/n1'])

  fireEvent.click(archived.getByRole('button', { name: 'Restore Aratis' }))

  await waitFor(() =>
    expect(archived.queryByRole('article', { name: 'Aratis' })).not.toBeInTheDocument(),
  )
  expect(screen.getByRole('button', { name: 'Edit Aratis' })).toBeInTheDocument()
  expect(callsWith(fetchMock, 'POST').map(([url]) => url)).toEqual([
    '/api/gm/npcs/n1/restore',
  ])
})

test('a rejected save shows the server message and keeps the form open', async () => {
  stubApi({ npcs: [], saveError: 'A name is required.' })
  renderManager()
  fireEvent.click(await screen.findByRole('button', { name: 'Add NPC' }))

  fireEvent.click(screen.getByRole('button', { name: 'Save' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('A name is required.')
  expect(screen.getByLabelText('Name')).toBeInTheDocument()
})

test('a load error shows a message', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(new Response(null, { status: 500 }))),
  )

  renderManager()

  expect(await screen.findByText('Could not load the NPCs.')).toBeInTheDocument()
})
