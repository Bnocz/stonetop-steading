export interface Npc {
  id: string
  name: string
  description: string
  gmNotes: string
  gmOnly: boolean
  archived: boolean
}

export interface NpcFields {
  name: string
  description: string
  gmNotes: string
  gmOnly: boolean
}

export interface PlayerNpc {
  id: string
  name: string
  description: string
}

async function readErrorDetail(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    return body.detail ?? 'Saving failed. Try again.'
  } catch {
    return 'Saving failed. Try again.'
  }
}

async function sendNpc(url: string, init: RequestInit): Promise<Npc> {
  const response = await fetch(url, init)
  if (!response.ok) throw new Error(await readErrorDetail(response))
  return (await response.json()) as Npc
}

function jsonBody(method: string, fields: NpcFields): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fields),
  }
}

export async function fetchNpcs(): Promise<Npc[]> {
  const response = await fetch('/api/gm/npcs')
  if (!response.ok) {
    throw new Error(`Loading the NPCs failed with HTTP ${response.status}`)
  }
  return (await response.json()) as Npc[]
}

export function createNpc(fields: NpcFields): Promise<Npc> {
  return sendNpc('/api/gm/npcs', jsonBody('POST', fields))
}

export function updateNpc(id: string, fields: NpcFields): Promise<Npc> {
  return sendNpc(`/api/gm/npcs/${id}`, jsonBody('PUT', fields))
}

export function archiveNpc(id: string): Promise<Npc> {
  return sendNpc(`/api/gm/npcs/${id}`, { method: 'DELETE' })
}

export function restoreNpc(id: string): Promise<Npc> {
  return sendNpc(`/api/gm/npcs/${id}/restore`, { method: 'POST' })
}

export async function fetchPlayerNpcs(token: string): Promise<PlayerNpc[]> {
  const response = await fetch(`/api/view/${token}/npcs`)
  if (!response.ok) {
    throw new Error(`Loading the NPCs failed with HTTP ${response.status}`)
  }
  return (await response.json()) as PlayerNpc[]
}
