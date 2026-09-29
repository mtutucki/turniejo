// Ochrona stron prywatnych — logikę "co jest publiczne" trzyma callback `authorized` w auth.ts.
// Niezalogowany user trafia na /login?callbackUrl=... (pages.signIn).
export { auth as proxy } from "@/auth"

export const config = {
  // Pomijamy route handlery Auth.js, proxy do backendu (/backend) i zasoby statyczne.
  matcher: [
    "/((?!api|backend|_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|webp|ico)$).*)",
  ],
}
