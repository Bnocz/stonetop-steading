import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { shareTokenFromPath } from './api/view.ts'
import '@fontsource/alegreya/latin-400.css'
import '@fontsource/alegreya/latin-400-italic.css'
import '@fontsource/alegreya/latin-700.css'
import '@fontsource/alegreya-sc/latin-400.css'
import '@fontsource/alegreya-sc/latin-700.css'
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
