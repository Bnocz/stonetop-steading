export interface HealthResponse {
  status: string
}

export async function fetchHealth(): Promise<HealthResponse> {
  const response = await fetch('/health')
  if (!response.ok) {
    throw new Error(`Health check failed with HTTP ${response.status}`)
  }
  return (await response.json()) as HealthResponse
}
