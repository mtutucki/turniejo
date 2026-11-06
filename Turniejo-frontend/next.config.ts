import type { NextConfig } from "next"

const nextConfig: NextConfig = {
  // E2E (Playwright) buduje aplikację z mockiem backendu do osobnego katalogu — nie koliduje z `next dev`.
  distDir: process.env.NEXT_DIST_DIR || ".next",
  // Obraz Dockera kopiuje tylko .next/standalone (server.js + wytrasowane node_modules) — bez tego
  // do produkcji szłyby całe node_modules z devDependencies. Tylko w Dockerfile: `next start`
  // (lokalnie i w E2E) nie obsługuje trybu standalone.
  output: process.env.NEXT_OUTPUT === "standalone" ? "standalone" : undefined,
  // Bez optymalizatora obrazków dla adresów zewnętrznych: z listą "dowolny host https" serwer pobierał
  // i przetwarzał obrazek z każdego linku, jaki ktoś podał w /_next/image. Wgrane herby są już
  // zmniejszone w przeglądarce do 256 px, a linki zewnętrzne przeglądarka ładuje sama (TeamLogo).
  // Backend nie ma skonfigurowanego CORS, więc przeglądarka rozmawia z nim przez Next.js
  // (ten sam origin). Prefiks /backend zamiast /api, bo /api/auth/* należy do Auth.js.
  // Uwaga: rewrites() liczy się w `next build` i ląduje w routes-manifest — API_URL musi być ustawiony
  // już przy buildzie (w Dockerfile jako build arg), zmiana samej zmiennej przy starcie nic nie da.
  async rewrites() {
    if (!process.env.API_URL) throw new Error("API_URL must be set at build time (used by /backend rewrite).")
    return [
      { source: "/backend/:path*", destination: `${process.env.API_URL}/:path*` },
      // Wgrane herby drużyn (LogoUrl = /uploads/team-logos/...), serwowane przez API.
      { source: "/uploads/:path*", destination: `${process.env.API_URL}/uploads/:path*` },
    ]
  },
}

export default nextConfig
