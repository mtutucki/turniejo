import NextAuth from "next-auth"
import type { JWT } from "next-auth/jwt"
import Keycloak from "next-auth/providers/keycloak"

import { isPublicPath } from "@/lib/auth/public-paths"

const issuer = `${process.env.KEYCLOAK_URL}/realms/${process.env.KEYCLOAK_REALM}`
const clientId = process.env.KEYCLOAK_CLIENT_ID!
const tokenEndpoint = `${issuer}/protocol/openid-connect/token`

// Odświeżamy z zapasem, żeby token nie wygasł "w locie" między proxy a backendem.
const REFRESH_MARGIN_MS = 30_000

async function refreshAccessToken(token: JWT): Promise<JWT> {
  try {
    const response = await fetch(tokenEndpoint, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "refresh_token",
        client_id: clientId,
        refresh_token: token.refreshToken!,
      }),
    })
    const refreshed = await response.json()
    if (!response.ok) throw refreshed

    return {
      ...token,
      accessToken: refreshed.access_token,
      expiresAt: Date.now() + refreshed.expires_in * 1000,
      // Keycloak rotuje refresh token — gdy nie przyśle nowego, stary nadal jest ważny.
      refreshToken: refreshed.refresh_token ?? token.refreshToken,
      error: undefined,
    }
  } catch (error) {
    console.error("Keycloak refresh_token failed", error)
    return { ...token, error: "RefreshTokenError" }
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  secret: process.env.NEXTAUTH_SECRET,
  providers: [
    Keycloak({
      clientId,
      issuer,
      // turniejo-frontend to klient publiczny (bez secretu) — zabezpiecza go PKCE.
      client: { token_endpoint_auth_method: "none" },
      checks: ["pkce", "state"],
    }),
  ],
  pages: { signIn: "/login" },
  session: { strategy: "jwt" },
  callbacks: {
    // Wywoływany przez proxy.ts — decyduje, czy strona wymaga zalogowania.
    authorized({ auth, request }) {
      if (isPublicPath(request.nextUrl.pathname)) return true
      return !!auth?.user && !auth.error
    },
    async jwt({ token, account, profile }) {
      if (account) {
        return {
          ...token,
          // Bez adaptera Auth.js nadaje userowi losowe id — backend identyfikuje usera po `sub` z Keycloaka.
          sub: profile?.sub ?? token.sub,
          accessToken: account.access_token,
          refreshToken: account.refresh_token,
          expiresAt: account.expires_at ? account.expires_at * 1000 : Date.now() + 300_000,
          error: undefined,
        }
      }

      if (token.expiresAt && Date.now() < token.expiresAt - REFRESH_MARGIN_MS) return token
      if (!token.refreshToken) return { ...token, error: "RefreshTokenError" }
      return refreshAccessToken(token)
    },
    async session({ session, token }) {
      session.accessToken = token.accessToken
      session.accessTokenExpiresAt = token.expiresAt
      session.error = token.error
      if (token.sub) session.user.id = token.sub
      return session
    },
  },
  events: {
    // Samo usunięcie ciasteczka zostawiłoby aktywną sesję SSO w Keycloaku — kolejne
    // "Zaloguj" wpuściłoby usera bez hasła. Zamykamy ją kanałem back-channel refresh tokenem.
    async signOut(message) {
      const token = "token" in message ? message.token : null
      if (!token?.refreshToken) return
      await fetch(`${issuer}/protocol/openid-connect/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({ client_id: clientId, refresh_token: token.refreshToken }),
      }).catch((error) => console.error("Keycloak logout failed", error))
    },
  },
})
