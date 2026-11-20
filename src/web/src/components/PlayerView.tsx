import { useQuery } from '@tanstack/react-query'
import { formatStatValue } from '../api/steading.ts'
import { fetchPlayerLog, fetchPlayerSteading } from '../api/view.ts'
import LogList from './LogList.tsx'
import PlayerMap from './PlayerMap.tsx'
import PlayerNpcList from './PlayerNpcList.tsx'
import Tabs from './Tabs.tsx'

function PlayerView({ token }: { token: string }) {
  const steading = useQuery({
    queryKey: ['view', token, 'steading'],
    queryFn: () => fetchPlayerSteading(token),
  })
  const log = useQuery({
    queryKey: ['view', token, 'log'],
    queryFn: () => fetchPlayerLog(token),
    enabled: Boolean(steading.data),
  })

  if (steading.isPending) return <p>Loading the steading…</p>
  if (steading.isError) return <p>Could not load the steading.</p>
  if (!steading.data) return <p>This link isn't valid. Ask your GM for a new one.</p>

  const { name, stats, debilities } = steading.data

  return (
    <main>
      <h1>{name}</h1>
      <Tabs
        label="Sections"
        tabs={[
          {
            label: 'Steading',
            content: (
              <>
                <div className="table-scroll">
                  <table className="stat-block">
                    <thead>
                      <tr>
                        <th scope="col">Stat</th>
                        <th scope="col" className="num">
                          Value
                        </th>
                        <th scope="col">Label</th>
                      </tr>
                    </thead>
                    <tbody>
                      {stats.map((stat) => (
                        <tr key={stat.stat}>
                          <th scope="row">{stat.stat}</th>
                          <td className="num">{formatStatValue(stat.stat, stat.value)}</td>
                          <td className="label">{stat.label}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <p className="debilities">Debilities: {debilities.length > 0 ? debilities.join(', ') : 'none'}</p>
                <h2>Change log</h2>
                {log.isError && <p>Could not load the change log.</p>}
                {log.data && <LogList entries={log.data} />}
              </>
            ),
          },
          { label: 'NPCs', content: <PlayerNpcList token={token} /> },
          { label: 'Map', content: <PlayerMap token={token} /> },
        ]}
      />
    </main>
  )
}

export default PlayerView
