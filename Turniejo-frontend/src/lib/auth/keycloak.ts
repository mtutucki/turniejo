import "server-only"

const issuer = `${process.env.KEYCLOAK_URL}/realms/${process.env.KEYCLOAK_REALM}`
const clientId = process.env.KEYCLOAK_CLIENT_ID!
// turniejo-frontend jest klientem publicznym — secret jest opcjonalny (dla klienta confidential).
const clientSecret = process.env.KEYCLOAK_CLIENT_SECRET

export type KeycloakTokens = {
  accessToken: string
  refreshToken: string
  /** Epoch w milisekundach. */
  expiresAt: number
}

export type AccessTokenClaims = {
  sub: string
  email?: string
  name?: string
}

export class KeycloakAuthError extends Error {
  constructor(
    readonly error: string,
    readonly description?: string,
  ) {
    super(description ?? error)
    this.name = "KeycloakAuthError"
  }
}

function clientParams(params: Record<string, string>) {
  const body = new URLSearchParams({ client_id: clientId, ...params })
  if (clientSecret) body.set("client_secret", clientSecret)
  return body
}

async function tokenRequest(params: Record<string, string>): Promise<KeycloakTokens> {
  const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: clientParams(params),
    cache: "no-store",
  })
  const data = await response.json()
  if (!response.ok) throw new KeycloakAuthError(data.error, data.error_description)

  return {
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
    expiresAt: Date.now() + data.expires_in * 1000,
  }
}

/** Resource Owner Password grant — logowanie z własnego formularza, bez strony Keycloaka. */
export function passwordGrant(username: string, password: string) {
  return tokenRequest({ grant_type: "password", username, password, scope: "openid" })
}

export function refreshGrant(refreshToken: string) {
  return tokenRequest({ grant_type: "refresh_token", refresh_token: refreshToken })
}

/** Zamyka sesję usera w Keycloaku (unieważnia refresh token). */
export async function endSession(refreshToken: string) {
  await fetch(`${issuer}/protocol/openid-connect/logout`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: clientParams({ refresh_token: refreshToken }),
  })
}

/** Payload JWT bez weryfikacji podpisu — token przyszedł przed chwilą bezpośrednio z Keycloaka. */
export function readAccessTokenClaims(accessToken: string): AccessTokenClaims {
  const payload = accessToken.split(".")[1]!
  return JSON.parse(Buffer.from(payload, "base64url").toString("utf8"))
}
