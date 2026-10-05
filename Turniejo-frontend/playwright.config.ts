import { defineConfig, devices } from "@playwright/test"

const MOCK_PORT = 4010
const APP_PORT = 3100
const MOCK_URL = `http://localhost:${MOCK_PORT}`

/**
 * E2E na produkcyjnym buildzie Next.js z mockiem backendu (MSW, e2e/mock) — bez .NET i Keycloaka.
 * Auth.js loguje przez mockowy token endpoint, SSR portalu i rewrite /backend trafiają do mocka,
 * SignalR też jest mockowany (WebSockety), więc da się sprawdzić wynik "na żywo".
 */
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  // Wspólny stan mocka (reset przed każdym testem) — testy lecą po kolei.
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 30_000,
  expect: { timeout: 8_000 },
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: `http://localhost:${APP_PORT}`,
    locale: "pl-PL",
    timezoneId: "Europe/Warsaw",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    // Aplikacja jest mobile-first (sędzia na boisku, kibic na trybunach) — główny projekt to telefon.
    { name: "mobile", use: { ...devices["Pixel 7"], channel: "chrome" } },
    { name: "desktop", use: { ...devices["Desktop Chrome"], channel: "chrome" } },
  ],
  webServer: [
    {
      command: "npx tsx e2e/mock/server.ts",
      url: `${MOCK_URL}/__control/state`,
      env: { MOCK_PORT: String(MOCK_PORT) },
      reuseExistingServer: !process.env.CI,
    },
    {
      command: `npx next build && npx next start -p ${APP_PORT}`,
      url: `http://localhost:${APP_PORT}/login`,
      timeout: 300_000,
      reuseExistingServer: !process.env.CI,
      env: {
        NEXT_DIST_DIR: ".next-e2e",
        API_URL: MOCK_URL,
        KEYCLOAK_URL: MOCK_URL,
        KEYCLOAK_REALM: "turniejo",
        KEYCLOAK_CLIENT_ID: "turniejo-frontend",
        KEYCLOAK_CLIENT_SECRET: "",
        NEXTAUTH_SECRET: "e2e-only-secret-not-for-production-0123456789",
        NEXTAUTH_URL: `http://localhost:${APP_PORT}`,
        AUTH_TRUST_HOST: "true",
      },
    },
  ],
})
