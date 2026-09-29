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
