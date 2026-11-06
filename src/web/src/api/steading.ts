export interface Stat {
  stat: string
  raw: number
  effective: number
  label: string | null
  min: number
  max: number | null
}

export interface Steading {
  name: string
  stats: Stat[]
  debilities: string[]
}

export interface LogEntry {
  stat: string
  oldValue: number
  newValue: number
  changedAt: string
}

export async function fetchSteading(): Promise<Steading> {
  const response = await fetch('/api/gm/steading')
  if (!response.ok) {
    throw new Error(`Loading the steading failed with HTTP ${response.status}`)
  }
  return (await response.json()) as Steading
}

export async function fetchLog(): Promise<LogEntry[]> {
  const response = await fetch('/api/gm/steading/log')
  if (!response.ok) {
    throw new Error(`Loading the log failed with HTTP ${response.status}`)
  }
  return (await response.json()) as LogEntry[]
}

async function readErrorDetail(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    return body.detail ?? 'Saving failed. Try again.'
  } catch {
    return 'Saving failed. Try again.'
  }
}

export async function changeStat(stat: string, value: number): Promise<Steading> {
  const response = await fetch(`/api/gm/steading/stats/${stat}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ value }),
  })
  if (!response.ok) throw new Error(await readErrorDetail(response))
  return (await response.json()) as Steading
}

// Surplus is a count; every other stat is a modifier, shown with its sign.
export function formatStatValue(stat: string, value: number): string {
  if (stat === 'Surplus') return String(value)
  return value >= 0 ? `+${value}` : String(value)
}
