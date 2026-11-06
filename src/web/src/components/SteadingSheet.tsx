import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { changeStat, fetchSteading } from '../api/steading.ts'
import StatRow from './StatRow.tsx'

function SteadingSheet() {
  const queryClient = useQueryClient()
  const steading = useQuery({ queryKey: ['steading'], queryFn: fetchSteading })
  const edit = useMutation({
    mutationFn: ({ stat, value }: { stat: string; value: number }) =>
      changeStat(stat, value),
    onSuccess: (updated) => {
      queryClient.setQueryData(['steading'], updated)
      return queryClient.invalidateQueries({ queryKey: ['steading-log'] })
    },
    // A failed save may mean the sheet on screen is out of date.
    onError: () => queryClient.invalidateQueries({ queryKey: ['steading'] }),
  })

  if (steading.isPending) return <p>Loading the steading…</p>
  if (steading.isError) return <p>Could not load the steading.</p>

  const { name, stats, debilities } = steading.data

  return (
    <section>
      <h2>{name}</h2>
      {edit.isError && <p role="alert">{edit.error.message}</p>}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th scope="col">Stat</th>
              <th scope="col">Value</th>
              <th scope="col">Effective</th>
              <th scope="col">Label</th>
              <th scope="col">Change</th>
            </tr>
          </thead>
          <tbody>
            {stats.map((stat) => (
              <StatRow
                key={stat.stat}
                stat={stat}
                disabled={edit.isPending}
                onChange={(value) => edit.mutate({ stat: stat.stat, value })}
              />
            ))}
          </tbody>
        </table>
      </div>
      <p>Debilities: {debilities.length > 0 ? debilities.join(', ') : 'none'}</p>
    </section>
  )
}

export default SteadingSheet
