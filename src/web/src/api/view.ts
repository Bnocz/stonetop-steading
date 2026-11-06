import type { LogEntry } from './steading.ts'

export interface PlayerStat {
  stat: string
  value: number
  label: string | null
}

export interface PlayerSteading {
  name: string
  stats: PlayerStat[]
  debilities: string[]
}

export function shareTokenFromPath(pathname: string): string | null {
  return /^\/view\/([A-Za-z0-9_-]+)\/?$/.exec(pathname)?.[1] ?? null
}

// Returns null for an unknown token, so the page can say the link isn't valid.
export async function fetchPlayerSteading(token: string): Promise<PlayerSteading | null> {
  const response = await fetch(`/api/view/${token}/steading`)
  if (response.status === 404) return null
  if (!response.ok) {
    throw new Error(`Loading the steading failed with HTTP ${response.status}`)
  }
  return (await response.json()) as PlayerSteading
}

export async function fetchPlayerLog(token: string): Promise<LogEntry[]> {
  const response = await fetch(`/api/view/${token}/log`)
  if (!response.ok) {
    throw new Error(`Loading the log failed with HTTP ${response.status}`)
  }
  return (await response.json()) as LogEntry[]
}
