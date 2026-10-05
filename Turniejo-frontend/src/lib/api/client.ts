import { ApiError } from "./errors"

type ApiRequestOptions = Omit<RequestInit, "body"> & {
  /** Access token z sesji Auth.js — dołączany jako Bearer. */
  token?: string
  /** Obiekt serializowany do JSON. */
  body?: unknown
}

// W przeglądarce idziemy przez rewrite /backend (brak CORS na backendzie),
// na serwerze Next.js bezpośrednio do API.
function resolveUrl(path: string) {
  const base = typeof window === "undefined" ? process.env.API_URL : "/backend"
  return `${base}${path}`
}

export async function apiFetch<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const { token, body, headers, ...init } = options

  const requestHeaders = new Headers(headers)
  requestHeaders.set("Accept", "application/json")
  if (token) requestHeaders.set("Authorization", `Bearer ${token}`)
  if (body !== undefined) requestHeaders.set("Content-Type", "application/json")

  const response = await fetch(resolveUrl(path), {
    ...init,
    headers: requestHeaders,
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) throw await ApiError.fromResponse(response)
  if (response.status === 204) return undefined as T

  const contentType = response.headers.get("Content-Type") ?? ""
  return (contentType.includes("json") ? response.json() : response.text()) as Promise<T>
}
