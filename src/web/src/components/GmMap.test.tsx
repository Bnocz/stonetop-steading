import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import GmMap from './GmMap.tsx'

interface FakeWaypoint {
  x: number
  y: number
  label: string
}

interface FakeSession {
  id: string
  number: number
  title: string
  gmOnly: boolean
  waypoints: FakeWaypoint[]
}

function seedSessions(): FakeSession[] {
  return [
    { id: 's1', number: 1, title: 'The Wedding', gmOnly: false, waypoints: [{ x: 0.69, y: 0.49, label: 'Stonetop' }] },
    { id: 's2', number: 2, title: 'Into the Wood', gmOnly: false, waypoints: [] },
    { id: 's3', number: 3, title: 'The Ruined Tower', gmOnly: true, waypoints: [] },
  ]
}

function stubApi(options: { image?: boolean; sessions?: FakeSession[]; saveError?: string } = {}) {
  const sessions = options.sessions ?? seedSessions()
  let image = options.image ?? true
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    if (url === '/api/gm/map') {
      return Promise.resolve(
        Response.json({ image: image ? { version: 'v1', contentType: 'image/png' } : null }),
      )
    }
    if (url === '/api/gm/sessions' && method === 'GET') {
      return Promise.resolve(Response.json([...sessions].sort((a, b) => a.number - b.number)))
    }
    if (options.saveError && method !== 'GET') {
      return Promise.resolve(Response.json({ detail: options.saveError }, { status: 400 }))
    }
    if (url === '/api/gm/map/image' && method === 'PUT') {
      image = true
      return Promise.resolve(Response.json({ version: 'v2', contentType: 'image/png' }))
    }
    if (url === '/api/gm/sessions' && method === 'POST') {
      const body = JSON.parse(init!.body as string) as Omit<FakeSession, 'id' | 'waypoints'>
      const created = { ...body, title: body.title, id: `s${sessions.length + 1}`, waypoints: [] }
      sessions.push(created)
      return Promise.resolve(Response.json(created, { status: 201 }))
    }
    const match = /^\/api\/gm\/sessions\/([^/]+)(\/waypoints)?$/.exec(url)
    const session = sessions.find((s) => s.id === match?.[1])
    if (session && method === 'PUT' && match?.[2]) {
      session.waypoints = (JSON.parse(init!.body as string) as { waypoints: FakeWaypoint[] }).waypoints
      return Promise.resolve(Response.json(session))
    }
    if (session && method === 'PUT') {
      Object.assign(session, JSON.parse(init!.body as string))
      return Promise.resolve(Response.json(session))
    }
    if (session && method === 'DELETE') {
      sessions.splice(sessions.indexOf(session), 1)
      return Promise.resolve(new Response(null, { status: 204 }))
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

afterEach(() => {
  vi.restoreAllMocks()
})

function renderMap() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <GmMap />
    </QueryClientProvider>,
  )
}

function callsWith(fetchMock: ReturnType<typeof stubApi>, method: string) {
  return fetchMock.mock.calls.filter(([, init]) => (init?.method ?? 'GET') === method)
}

function clickMapAt(x: number, y: number) {
  const map = screen.getByLabelText('Region map')
  map.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width: 100, height: 100, right: 100, bottom: 100, x: 0, y: 0 }) as DOMRect
  fireEvent.click(map, { clientX: x * 100, clientY: y * 100 })
}

test('without a map it asks for one and uploads the chosen file', async () => {
  const fetchMock = stubApi({ image: false })
  renderMap()
  const file = new File(['png-bytes'], 'region.png', { type: 'image/png' })

  fireEvent.change(await screen.findByLabelText('Map image'), { target: { files: [file] } })

  await screen.findByLabelText('Region map')
  const [[url, init]] = callsWith(fetchMock, 'PUT')
  expect(url).toBe('/api/gm/map/image')
  expect(init!.body).toBe(file)
  expect(init!.headers).toEqual({ 'Content-Type': 'image/png' })
})

test('opens on the latest session and the arrows step through them', async () => {
  stubApi()
  renderMap()

  expect(await screen.findByRole('heading', { name: 'Session 3: The Ruined Tower' })).toBeInTheDocument()
  expect(screen.getByText('3 of 3')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Next session' })).toBeDisabled()

  fireEvent.click(screen.getByRole('button', { name: 'Previous session' }))
  fireEvent.click(screen.getByRole('button', { name: 'Previous session' }))

  expect(screen.getByRole('heading', { name: 'Session 1: The Wedding' })).toBeInTheDocument()
  expect(screen.getByText('1 of 3')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Previous session' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Next session' })).toBeEnabled()
})

test('the map image is the GM image with its version', async () => {
  stubApi()
  renderMap()

  await screen.findByRole('heading', { name: 'Session 3: The Ruined Tower' })

  expect(document.querySelector('img')).toHaveAttribute('src', '/api/gm/map/image?v=v1')
})

test('hidden sessions are marked', async () => {
  stubApi()
  renderMap()

  expect(await screen.findByText('Hidden from players')).toBeInTheDocument()
  fireEvent.click(screen.getByRole('button', { name: 'Previous session' }))
  expect(screen.queryByText('Hidden from players')).not.toBeInTheDocument()
})

test('says when there are no sessions yet', async () => {
  stubApi({ sessions: [] })
  renderMap()

  expect(await screen.findByText('No sessions yet.')).toBeInTheDocument()
})

test('a new session takes the next number, starts hidden, and is shown once saved', async () => {
  const fetchMock = stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'New session' }))

  expect(screen.getByLabelText('Number')).toHaveValue(4)
  expect(screen.getByLabelText('Hidden from players')).toBeChecked()
  fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Blackwater' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save session' }))

  expect(await screen.findByRole('heading', { name: 'Session 4: Blackwater' })).toBeInTheDocument()
  const [[, init]] = callsWith(fetchMock, 'POST')
  expect(JSON.parse(init!.body as string)).toEqual({ number: 4, title: 'Blackwater', gmOnly: true })
})

test('editing a session sends its number, title and visibility', async () => {
  const fetchMock = stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit session' }))

  expect(screen.getByLabelText('Title')).toHaveValue('The Ruined Tower')
  fireEvent.click(screen.getByLabelText('Hidden from players'))
  fireEvent.click(screen.getByRole('button', { name: 'Save session' }))

  await waitFor(() => expect(screen.queryByText('Hidden from players')).not.toBeInTheDocument())
  const [[url, init]] = callsWith(fetchMock, 'PUT')
  expect(url).toBe('/api/gm/sessions/s3')
  expect(JSON.parse(init!.body as string)).toEqual({ number: 3, title: 'The Ruined Tower', gmOnly: false })
})

test('editing the route: clicks add stops, labels and undo work, and save sends the route', async () => {
  const fetchMock = stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit route' }))

  clickMapAt(0.5, 0.5)
  clickMapAt(0.25, 0.75)
  clickMapAt(0.9, 0.9)
  fireEvent.click(screen.getByRole('button', { name: 'Undo' }))
  fireEvent.change(screen.getByLabelText('Label for stop 2'), { target: { value: 'The Ruined Tower' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save route' }))

  await waitFor(() => expect(screen.queryByRole('button', { name: 'Save route' })).not.toBeInTheDocument())
  const [[url, init]] = callsWith(fetchMock, 'PUT')
  expect(url).toBe('/api/gm/sessions/s3/waypoints')
  expect(JSON.parse(init!.body as string)).toEqual({
    waypoints: [
      { x: 0.5, y: 0.5, label: '' },
      { x: 0.25, y: 0.75, label: 'The Ruined Tower' },
    ],
  })
  expect(screen.getByLabelText('Stop 2: The Ruined Tower')).toBeInTheDocument()
})

test('a stop can be moved or removed while editing', async () => {
  const fetchMock = stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'Previous session' }))
  fireEvent.click(screen.getByRole('button', { name: 'Previous session' }))
  fireEvent.click(screen.getByRole('button', { name: 'Edit route' }))

  clickMapAt(0.5, 0.5)
  fireEvent.click(screen.getByRole('button', { name: 'Move stop 1' }))
  clickMapAt(0.1, 0.2)
  fireEvent.click(screen.getByRole('button', { name: 'Remove stop 2' }))
  fireEvent.click(screen.getByRole('button', { name: 'Save route' }))

  await waitFor(() => expect(callsWith(fetchMock, 'PUT')).toHaveLength(1))
  const [[url, init]] = callsWith(fetchMock, 'PUT')
  expect(url).toBe('/api/gm/sessions/s1/waypoints')
  expect(JSON.parse(init!.body as string)).toEqual({ waypoints: [{ x: 0.1, y: 0.2, label: 'Stonetop' }] })
})

test('cancelling route edits keeps the saved route', async () => {
  const fetchMock = stubApi()
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'Edit route' }))

  clickMapAt(0.5, 0.5)
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

  expect(screen.queryByLabelText('Stop 1')).not.toBeInTheDocument()
  expect(callsWith(fetchMock, 'PUT')).toEqual([])
})

test('clicking the map does nothing unless the route is being edited', async () => {
  const fetchMock = stubApi()
  renderMap()
  await screen.findByRole('heading', { name: 'Session 3: The Ruined Tower' })

  clickMapAt(0.5, 0.5)

  expect(screen.queryByLabelText('Stop 1')).not.toBeInTheDocument()
  expect(callsWith(fetchMock, 'PUT')).toEqual([])
})

test('deleting asks first and then removes the session', async () => {
  const fetchMock = stubApi()
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
  renderMap()

  fireEvent.click(await screen.findByRole('button', { name: 'Delete session' }))

  expect(confirm).toHaveBeenCalled()
  expect(await screen.findByRole('heading', { name: 'Session 2: Into the Wood' })).toBeInTheDocument()
  expect(callsWith(fetchMock, 'DELETE').map(([url]) => url)).toEqual(['/api/gm/sessions/s3'])
})

test('declining the delete confirmation keeps the session', async () => {
  const fetchMock = stubApi()
  vi.spyOn(window, 'confirm').mockReturnValue(false)
  renderMap()

  fireEvent.click(await screen.findByRole('button', { name: 'Delete session' }))

  expect(callsWith(fetchMock, 'DELETE')).toEqual([])
  expect(screen.getByRole('heading', { name: 'Session 3: The Ruined Tower' })).toBeInTheDocument()
})

test('a rejected save shows the server message', async () => {
  stubApi({ saveError: 'There is already a session 3.' })
  renderMap()
  fireEvent.click(await screen.findByRole('button', { name: 'New session' }))

  fireEvent.click(screen.getByRole('button', { name: 'Save session' }))

  expect(await screen.findByRole('alert')).toHaveTextContent('There is already a session 3.')
  expect(screen.getByLabelText('Number')).toBeInTheDocument()
})

test('a load error shows a message', async () => {
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(null, { status: 500 }))))
  renderMap()

  expect(await screen.findByText('Could not load the map.')).toBeInTheDocument()
})

test('the hidden marker belongs to the session heading area', async () => {
  stubApi()
  renderMap()

  const nav = await screen.findByRole('navigation', { name: 'Sessions' })
  expect(within(nav).getByText('Hidden from players')).toBeInTheDocument()
})

test('the selected session’s stops are listed under the map with where the party is', async () => {
  stubApi({
    sessions: [
      {
        id: 's1',
        number: 1,
        title: 'The Wedding',
        gmOnly: false,
        waypoints: [
          { x: 0.69, y: 0.49, label: 'Stonetop' },
          { x: 0.5, y: 0.5, label: '' },
          { x: 0.45, y: 0.8, label: 'The Ruined Tower' },
        ],
      },
    ],
  })
  renderMap()

  const stops = await screen.findByRole('list', { name: 'Stops' })

  expect(within(stops).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
    'Stonetop',
    'Unnamed stop',
    'The Ruined Tower (the party is here)',
  ])
})

test('a session without stops has no stop list', async () => {
  stubApi()
  renderMap()

  await screen.findByRole('heading', { name: 'Session 3: The Ruined Tower' })

  expect(screen.queryByRole('list', { name: 'Stops' })).not.toBeInTheDocument()
})
