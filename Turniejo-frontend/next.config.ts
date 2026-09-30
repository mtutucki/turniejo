import type { NextConfig } from "next"

const nextConfig: NextConfig = {
  // E2E (Playwright) buduje aplikację z mockiem backendu do osobnego katalogu — nie koliduje z `next dev`.
  distDir: process.env.NEXT_DIST_DIR || ".next",
  // Herby drużyn to dowolne adresy https od organizatora — optymalizator zmniejsza je do 36–48 px
  // (AVIF/WebP) zamiast ściągać na telefon pełne pliki. Next 16 domyślnie odmawia pobierania z adresów
  // lokalnych/prywatnych (dangerouslyAllowLocalIP=false), a limit rozmiaru chroni pamięć serwera.
  images: {
    remotePatterns: [{ protocol: "https", hostname: "**" }],
    formats: ["image/avif", "image/webp"],
    maximumResponseBody: 5_000_000,
    minimumCacheTTL: 86_400,
  },
  // Backend nie ma skonfigurowanego CORS, więc przeglądarka rozmawia z nim przez Next.js
  // (ten sam origin). Prefiks /backend zamiast /api, bo /api/auth/* należy do Auth.js.
  async rewrites() {
    return [{ source: "/backend/:path*", destination: `${process.env.API_URL}/:path*` }]
  },
}

export default nextConfig
