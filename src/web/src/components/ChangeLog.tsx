import { useQuery } from '@tanstack/react-query'
import { fetchLog, formatStatValue } from '../api/steading.ts'

function ChangeLog() {
  const log = useQuery({ queryKey: ['steading-log'], queryFn: fetchLog })

  return (
    <section>
      <h2>Change log</h2>
      {log.isError && <p>Could not load the change log.</p>}
      {log.data?.length === 0 && <p>No changes yet.</p>}
      {log.data && log.data.length > 0 && (
        <ul>
          {log.data.map((entry) => (
            <li key={`${entry.changedAt}-${entry.stat}-${entry.newValue}`}>
              {entry.stat}: {formatStatValue(entry.stat, entry.oldValue)} →{' '}
              {formatStatValue(entry.stat, entry.newValue)} (
              {new Date(entry.changedAt).toLocaleString()})
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

export default ChangeLog
