export function readUrlParam(key: string): string | null {
  return new URLSearchParams(window.location.search).get(key)
}

export function writeUrlParams(patch: Record<string, string | null>): void {
  const params = new URLSearchParams(window.location.search)

  for (const [key, value] of Object.entries(patch)) {
    if (value === null || value === '') {
      params.delete(key)
    } else {
      params.set(key, value)
    }
  }

  const query = params.toString()
  window.history.replaceState(null, '', query ? `${window.location.pathname}?${query}` : window.location.pathname)
}
