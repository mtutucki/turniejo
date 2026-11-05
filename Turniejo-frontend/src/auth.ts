import NextAuth, { CredentialsSignin } from "next-auth"
import type { JWT } from "next-auth/jwt"
import Credentials from "next-auth/providers/credentials"
import { z } from "zod"

import {
  endSession,
  KeycloakAuthError,
  passwordGrant,
  readAccessTokenClaims,
  refreshGrant,
} from "@/lib/auth/keycloak"
import { isPublicPath, isStartPath } from "@/lib/auth/public-paths"

// Odświeżamy z zapasem, żeby token nie wygasł "w locie" między proxy a backendem.
const REFRESH_MARGIN_MS = 30_000

const credentialsSchema = z.object({
  email: z.string().trim().min(1),
  password: z.string().min(1),
})

/** Kody błędów logowania — `result.code` z signIn() po stronie formularza. */
export type LoginErrorCode = "invalid_credentials" | "account_not_ready"

class LoginError extends CredentialsSignin {
  constructor(code: LoginErrorCode) {
    super()
    this.code = code
  }
}

async function refreshAccessToken(token: JWT): Promise<JWT> {
  try {
    const refreshed = await refreshGrant(token.refreshToken!)
    return { ...token, ...refreshed, error: undefined }
  } catch (error) {
    // invalid_grant = sesja w Keycloaku wygasła lub została zamknięta — zwykłe wylogowanie, nie awaria.
    const sessionExpired = error instanceof KeycloakAuthError && error.error === "invalid_grant"
    if (!sessionExpired) console.error("Keycloak refresh_token failed", error)
    return { ...token, error: "RefreshTokenError" }
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  secret: process.env.NEXTAUTH_SECRET,
  providers: [
    Credentials({
      credentials: { email: {}, password: {} },
      async authorize(credentials) {
        const parsed = credentialsSchema.safeParse(credentials)
        if (!parsed.success) throw new LoginError("invalid_credentials")

        try {
          const tokens = await passwordGrant(parsed.data.email, parsed.data.password)
          const claims = readAccessTokenClaims(tokens.accessToken)
          // id = `sub` z Keycloaka — tak samo identyfikuje usera backend (ICurrentUserContext).
          return { id: claims.sub, email: claims.email, name: claims.name, ...tokens }
        } catch (error) {
          if (!(error instanceof KeycloakAuthError)) throw error
          // Np. wymagana akcja w Keycloaku (ustawienie hasła po resecie, weryfikacja emaila).
          if (error.description === "Account is not fully set up") {
            throw new LoginError("account_not_ready")
          }
          throw new LoginError("invalid_credentials")
        }
      },
    }),
  ],
  pages: { signIn: "/login" },
  session: { strategy: "jwt" },
  callbacks: {
    // Wywoływany przez proxy.ts — decyduje, czy strona wymaga zalogowania.
    authorized({ auth, request }) {
      const { pathname } = request.nextUrl
      if (isPublicPath(pathname)) return true
      if (auth?.user && !auth.error) return true
      // Ze strony startowej i tak wracamy na start, więc callbackUrl tylko zaśmieca adres.
      if (isStartPath(pathname)) return Response.redirect(new URL("/login", request.nextUrl))
      return false
    },
    async jwt({ token, user, trigger, session }) {
      // useSession().update({ name }) po zmianie danych osobowych: nowe imię i nazwisko bez
      // ponownego logowania. Nic innego z klienta do tokenu nie wpuszczamy.
      if (trigger === "update" && typeof session?.name === "string") {
        return { ...token, name: session.name.slice(0, 201) }
      }

      if (user) {
        return {
          ...token,
          sub: user.id,
          accessToken: user.accessToken,
          refreshToken: user.refreshToken,
          expiresAt: user.expiresAt,
          error: undefined,
        }
      }

      if (token.expiresAt && Date.now() < token.expiresAt - REFRESH_MARGIN_MS) return token
      // Martwy refresh token nie ożyje — nie odpytujemy Keycloaka przy każdym żądaniu.
      if (token.error) return token
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
    // Samo usunięcie ciasteczka zostawiłoby ważny refresh token i sesję w Keycloaku.
    async signOut(message) {
      const token = "token" in message ? message.token : null
      if (!token?.refreshToken) return
      await endSession(token.refreshToken).catch((error) =>
        console.error("Keycloak logout failed", error),
      )
    },
  },
})
