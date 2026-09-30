import type { NextConfig } from "next"

const nextConfig: NextConfig = {
  // E2E (Playwright) buduje aplikację z mockiem backendu do osobnego katalogu — nie koliduje z `next dev`.
  distDir: process.env.NEXT_DIST_DIR || ".next",
  // Obraz Dockera kopiuje tylko .next/standalone (server.js + wytrasowane node_modules) — bez tego
  // do produkcji szłyby całe node_modules z devDependencies. Tylko w Dockerfile: `next start`
  // (lokalnie i w E2E) nie obsługuje trybu standalone.
  output: process.env.NEXT_OUTPUT === "standalone" ? "standalone" : undefined,
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
  // Uwaga: rewrites() liczy się w `next build` i ląduje w routes-manifest — API_URL musi być ustawiony
  // już przy buildzie (w Dockerfile jako build arg), zmiana samej zmiennej przy starcie nic nie da.
  async rewrites() {
    if (!process.env.API_URL) throw new Error("API_URL must be set at build time (used by /backend rewrite).")
    return [{ source: "/backend/:path*", destination: `${process.env.API_URL}/:path*` }]
  },
}

export default nextConfig
