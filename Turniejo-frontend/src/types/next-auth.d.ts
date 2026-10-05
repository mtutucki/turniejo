import type { DefaultSession } from "next-auth"

type AuthError = "RefreshTokenError"

declare module "next-auth" {
  interface Session {
    accessToken?: string
    /** Epoch w milisekundach — klient odświeża sesję przed wygaśnięciem (use-api.ts). */
    accessTokenExpiresAt?: number
    error?: AuthError
    user: { id: string } & DefaultSession["user"]
  }

  /** Zwracany przez authorize() w CredentialsProvider — tokeny trafiają do JWT sesji. */
  interface User {
    accessToken: string
    refreshToken: string
    expiresAt: number
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string
    refreshToken?: string
    /** Epoch w milisekundach. */
    expiresAt?: number
    error?: AuthError
  }
}
