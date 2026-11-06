import { useQuery } from '@tanstack/react-query'
import { fetchHealth } from './api/health.ts'
import GmArea from './components/GmArea.tsx'

function App() {
  const health = useQuery({ queryKey: ['health'], queryFn: fetchHealth })

  let status = 'checking…'
  if (health.isError) status = 'unavailable'
  else if (health.data) status = health.data.status

  return (
    <main>
      <h1>Stonetop Steading</h1>
      <p>API: {status}</p>
      <GmArea />
    </main>
  )
}

export default App
