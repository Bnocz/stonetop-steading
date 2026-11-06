import { formatStatValue, type Stat } from '../api/steading.ts'

interface StatRowProps {
  stat: Stat
  disabled: boolean
  onChange: (value: number) => void
}

function StatRow({ stat, disabled, onChange }: StatRowProps) {
  const atMax = stat.max !== null && stat.raw >= stat.max

  return (
    <tr>
      <th scope="row">{stat.stat}</th>
      <td>{formatStatValue(stat.stat, stat.raw)}</td>
      <td>{formatStatValue(stat.stat, stat.effective)}</td>
      <td>{stat.label}</td>
      <td>
        <button
          type="button"
          aria-label={`Lower ${stat.stat}`}
          disabled={disabled || stat.raw <= stat.min}
          onClick={() => onChange(stat.raw - 1)}
        >
          −
        </button>{' '}
        <button
          type="button"
          aria-label={`Raise ${stat.stat}`}
          disabled={disabled || atMax}
          onClick={() => onChange(stat.raw + 1)}
        >
          +
        </button>
      </td>
    </tr>
  )
}

export default StatRow
