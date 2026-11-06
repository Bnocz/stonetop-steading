import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { shareTokenFromPath } from './api/view.ts'
import './index.css'
import App from './App.tsx'
import PlayerView from './components/PlayerView.tsx'

const queryClient = new QueryClient()
const shareToken = shareTokenFromPath(window.location.pathname)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      {shareToken ? <PlayerView token={shareToken} /> : <App />}
    </QueryClientProvider>
  </StrictMode>,
)
