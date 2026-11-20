export interface Point {
  x: number
  y: number
}

export interface Waypoint extends Point {
  label: string
}

export interface PlaySession {
  id: string
  number: number
  title: string
  gmOnly: boolean
  waypoints: Waypoint[]
}

export interface SessionFields {
  number: number
  title: string
  gmOnly: boolean
}

export function sessionHeading(session: { number: number; title: string }): string {
  return session.title ? `Session ${session.number}: ${session.title}` : `Session ${session.number}`
}

export interface MapInfo {
  image: { version: string; contentType: string } | null
}

async function readErrorDetail(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    return body.detail ?? 'Saving failed. Try again.'
  } catch {
    return 'Saving failed. Try again.'
  }
}

async function send<T>(url: string, init: RequestInit): Promise<T> {
  const response = await fetch(url, init)
  if (!response.ok) throw new Error(await readErrorDetail(response))
  return (await response.json()) as T
}

function json(method: string, body: unknown): RequestInit {
  return { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }
}

export async function fetchMap(): Promise<MapInfo> {
  const response = await fetch('/api/gm/map')
  if (!response.ok) throw new Error(`Loading the map failed with HTTP ${response.status}`)
  return (await response.json()) as MapInfo
}

// The version changes with the file, so a replaced map is never served from cache.
export function gmMapImageUrl(version: string): string {
  return `/api/gm/map/image?v=${encodeURIComponent(version)}`
}

export function uploadMapImage(file: File): Promise<MapInfo['image']> {
  return send('/api/gm/map/image', {
    method: 'PUT',
    headers: { 'Content-Type': file.type || 'application/octet-stream' },
    body: file,
  })
}

export async function fetchSessions(): Promise<PlaySession[]> {
  const response = await fetch('/api/gm/sessions')
  if (!response.ok) throw new Error(`Loading the sessions failed with HTTP ${response.status}`)
  return (await response.json()) as PlaySession[]
}

export function createSession(fields: SessionFields): Promise<PlaySession> {
  return send('/api/gm/sessions', json('POST', fields))
}

export function updateSession(id: string, fields: SessionFields): Promise<PlaySession> {
  return send(`/api/gm/sessions/${id}`, json('PUT', fields))
}

export function saveRoute(id: string, waypoints: Waypoint[]): Promise<PlaySession> {
  return send(`/api/gm/sessions/${id}/waypoints`, json('PUT', { waypoints }))
}

export async function deleteSession(id: string): Promise<void> {
  const response = await fetch(`/api/gm/sessions/${id}`, { method: 'DELETE' })
  if (!response.ok) throw new Error(await readErrorDetail(response))
}
