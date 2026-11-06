import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import ChangeLog from './ChangeLog.tsx'
import SteadingSheet from './SteadingSheet.tsx'

interface FakeStat {
  stat: string
  raw: number
  effective: number
  label: string | null
  min: number
  max: number | null
}

function seedStats(): FakeStat[] {
  return [
    { stat: 'Size', raw: 0, effective: 0, label: 'Village', min: -1, max: 2 },
    { stat: 'Fortunes', raw: 3, effective: 3, label: null, min: -3, max: 3 },
    { stat: 'Surplus', raw: 0, effective: 0, label: null, min: 0, max: null },
    { stat: 'Prosperity', raw: -1, effective: -2, label: 'Dirt', min: -2, max: 2 },
    { stat: 'Defenses', raw: -1, effective: -1, label: 'Militia', min: -2, max: 3 },
    { stat: 'Population', raw: 0, effective: 0, label: 'Steady', min: -2, max: 2 },
  ]
}

function stubApi(options: { putError?: { status: number; detail: string } } = {}) {
  const stats = seedStats()
  const log: object[] = []
  const sheet = () => ({ name: 'Stonetop', stats, debilities: ['Lacking'] })
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url === '/api/gm/steading') return Promise.resolve(Response.json(sheet()))
    if (url === '/api/gm/steading/log') return Promise.resolve(Response.json(log))
    if (url.startsWith('/api/gm/steading/stats/') && init?.method === 'PUT') {
      if (options.putError) {
        return Promise.resolve(
          Response.json(
            { detail: options.putError.detail },
            { status: options.putError.status },
          ),
        )
      }
      const name = url.split('/').pop()
      const stat = stats.find((s) => s.stat === name)!
      const { value } = JSON.parse(init.body as string) as { value: number }
      log.unshift({
        stat: stat.stat,
        oldValue: stat.raw,
        newValue: value,
        changedAt: '2026-11-06T12:00:00Z',
      })
      stat.raw = value
      stat.effective = value
      return Promise.resolve(Response.json(sheet()))
    }
    return Promise.resolve(new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function renderSheet() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <SteadingSheet />
      <ChangeLog />
    </QueryClientProvider>,
  )
}

async function findRow(stat: string) {
  return within(await screen.findByRole('row', { name: new RegExp(`^${stat}\\b`) }))
}

test('shows raw value, effective value and label for each stat', async () => {
  stubApi()

  renderSheet()

  const prosperity = await findRow('Prosperity')
  expect(prosperity.getByRole('cell', { name: '-1' })).toBeInTheDocument()
  expect(prosperity.getByRole('cell', { name: '-2' })).toBeInTheDocument()
  expect(prosperity.getByRole('cell', { name: 'Dirt' })).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Stonetop' })).toBeInTheDocument()
})

test('modifiers show a sign and surplus does not', async () => {
  stubApi()

  renderSheet()

  const fortunes = await findRow('Fortunes')
  const surplus = await findRow('Surplus')
  expect(fortunes.getAllByRole('cell', { name: '+3' })).toHaveLength(2)
  expect(surplus.getAllByRole('cell', { name: '0' })).toHaveLength(2)
})

test('lists active debilities without controls to change them', async () => {
  stubApi()

  renderSheet()

  expect(await screen.findByText('Debilities: Lacking')).toBeInTheDocument()
  expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
})

test('editing a stat calls the API and refreshes the sheet and log', async () => {
  const fetchMock = stubApi()
  renderSheet()
  expect(await screen.findByText('No changes yet.')).toBeInTheDocument()
  const size = await findRow('Size')

  fireEvent.click(size.getByRole('button', { name: 'Raise Size' }))

  expect(await screen.findByText(/Size: \+0 → \+1/)).toBeInTheDocument()
  expect((await findRow('Size')).getAllByRole('cell', { name: '+1' })).toHaveLength(2)
  expect(fetchMock).toHaveBeenCalledWith(
    '/api/gm/steading/stats/Size',
    expect.objectContaining({ method: 'PUT', body: JSON.stringify({ value: 1 }) }),
  )
})

test('controls stop at the range the API sends', async () => {
  stubApi()

  renderSheet()

  const fortunes = await findRow('Fortunes')
  const surplus = await findRow('Surplus')
  expect(fortunes.getByRole('button', { name: 'Raise Fortunes' })).toBeDisabled()
  expect(fortunes.getByRole('button', { name: 'Lower Fortunes' })).toBeEnabled()
  expect(surplus.getByRole('button', { name: 'Lower Surplus' })).toBeDisabled()
  expect(surplus.getByRole('button', { name: 'Raise Surplus' })).toBeEnabled()
})

test('a failed edit shows the error from the API', async () => {
  stubApi({
    putError: {
      status: 409,
      detail: 'The steading changed while saving. Reload and try again.',
    },
  })
  renderSheet()
  const size = await findRow('Size')

  fireEvent.click(size.getByRole('button', { name: 'Raise Size' }))

  expect(await screen.findByRole('alert')).toHaveTextContent(
    'The steading changed while saving. Reload and try again.',
  )
})

test('a failed load shows an error', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(new Response(null, { status: 500 }))),
  )

  renderSheet()

  expect(await screen.findByText('Could not load the steading.')).toBeInTheDocument()
})
