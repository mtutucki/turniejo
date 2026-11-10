import type { MetadataRoute } from "next"

import { absoluteUrl } from "@/lib/site"

// Do Google trafia strona główna (lista turniejów), portale kibica i strony informacyjne. Panel,
// logowanie i rejestracja wymagają konta albo nic nie dają w wynikach — mają też noindex w layoutach.
// Portal ukryty z listy turniejów ma noindex w swoich metadanych, nie tutaj (adres i tak jest w /t/).
const PRIVATE = [
  "/start",
  "/tournaments",
  "/matches",
  "/teams",
  "/organizations",
  "/players",
  "/coach",
  "/parent",
  "/referee",
  "/organizer",
  "/account",
  "/notifications",
  "/join",
  "/joined",
  "/login",
  "/register",
  "/password-reset",
  "/backend",
  "/api",
]

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: "*", allow: "/", disallow: PRIVATE }],
    sitemap: absoluteUrl("/sitemap.xml"),
  }
}
