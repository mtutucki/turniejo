import type { MetadataRoute } from "next"

// Indeksujemy tylko publiczny portal kibica — panel aplikacji i tak wymaga logowania.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: "*", allow: ["/t/", "/login", "/register"], disallow: ["/"] }],
  }
}
