export interface Npc {
  id: string
  name: string
  description: string
  gmNotes: string
  gmOnly: boolean
  archived: boolean
  // Null (or missing) when the NPC has no portrait.
  portraitVersion?: string | null
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

// The version changes with the file, so a replaced portrait is never served from cache.
export function gmPortraitUrl(id: string, version: string): string {
  return `/api/gm/npcs/${id}/portrait?v=${encodeURIComponent(version)}`
}

export async function uploadPortrait(id: string, picture: Blob): Promise<{ version: string }> {
  const response = await fetch(`/api/gm/npcs/${id}/portrait`, {
    method: 'PUT',
    headers: { 'Content-Type': picture.type || 'application/octet-stream' },
    body: picture,
  })
  if (!response.ok) throw new Error(await readErrorDetail(response))
  return (await response.json()) as { version: string }
}

export async function removePortrait(id: string): Promise<void> {
  const response = await fetch(`/api/gm/npcs/${id}/portrait`, { method: 'DELETE' })
  if (!response.ok) throw new Error(await readErrorDetail(response))
}

export interface PlayerPortrait {
  npcId: string
  version: string
}

export async function fetchPlayerPortraits(token: string): Promise<PlayerPortrait[]> {
  const response = await fetch(`/api/view/${token}/npc-portraits`)
  if (!response.ok) throw new Error(`Loading the portraits failed with HTTP ${response.status}`)
  return (await response.json()) as PlayerPortrait[]
}

export function playerPortraitUrl(token: string, id: string, version: string): string {
  return `/api/view/${token}/npcs/${id}/portrait?v=${encodeURIComponent(version)}`
}

export async function fetchPlayerNpcs(token: string): Promise<PlayerNpc[]> {
  const response = await fetch(`/api/view/${token}/npcs`)
  if (!response.ok) {
    throw new Error(`Loading the NPCs failed with HTTP ${response.status}`)
  }
  return (await response.json()) as PlayerNpc[]
}
