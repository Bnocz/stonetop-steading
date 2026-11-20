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
      <td className="num">{formatStatValue(stat.stat, stat.raw)}</td>
      <td className={stat.effective === stat.raw ? 'num' : 'num changed'}>
        {formatStatValue(stat.stat, stat.effective)}
      </td>
      <td className="label">{stat.label}</td>
      <td className="actions">
        <button
          type="button"
          className="small"
          aria-label={`Lower ${stat.stat}`}
          disabled={disabled || stat.raw <= stat.min}
          onClick={() => onChange(stat.raw - 1)}
        >
          −
        </button>{' '}
        <button
          type="button"
          className="small"
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
