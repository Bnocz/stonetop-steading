import { formatStatValue, type LogEntry } from '../api/steading.ts'

function LogList({ entries }: { entries: LogEntry[] }) {
  if (entries.length === 0) return <p>No changes yet.</p>

  return (
    <ul>
      {entries.map((entry) => (
        <li key={`${entry.changedAt}-${entry.stat}-${entry.newValue}`}>
          {entry.stat}: {formatStatValue(entry.stat, entry.oldValue)} →{' '}
          {formatStatValue(entry.stat, entry.newValue)} (
          {new Date(entry.changedAt).toLocaleString()})
        </li>
      ))}
    </ul>
  )
}

export default LogList
