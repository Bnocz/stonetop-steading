import { useQuery } from '@tanstack/react-query'
import { fetchLog } from '../api/steading.ts'
import LogList from './LogList.tsx'

function ChangeLog() {
  const log = useQuery({ queryKey: ['steading-log'], queryFn: fetchLog })

  return (
    <section>
      <h2>Change log</h2>
      {log.isError && <p>Could not load the change log.</p>}
      {log.data && <LogList entries={log.data} />}
    </section>
  )
}

export default ChangeLog
