import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import App from './App.tsx'

function renderApp() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>,
  )
}

test('shows the API health status', async () => {
  const fetchMock = vi.fn(() => Promise.resolve(Response.json({ status: 'ok' })))
  vi.stubGlobal('fetch', fetchMock)

  renderApp()

  expect(await screen.findByText('API: ok')).toBeInTheDocument()
  expect(fetchMock).toHaveBeenCalledWith('/health')
})

test('shows an error when the API is unreachable', async () => {
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(null, { status: 503 }))))

  renderApp()

  expect(await screen.findByText('API: unavailable')).toBeInTheDocument()
})
