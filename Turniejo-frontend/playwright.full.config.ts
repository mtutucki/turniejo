import { defineConfig, devices } from "@playwright/test"

/**
 * E2E na pełnym stosie (faza 5): prawdziwy backend, Keycloak, Postgres i frontend z obrazów.
 * Stos stawia e2e-full/docker-compose.yml (patrz komentarz w tym pliku); testy zakładają własne
 * dane przez API, więc mogą działać na tym samym stosie wiele razy.
 */
export default defineConfig({
  testDir: "./e2e-full",
  testMatch: "**/*.spec.ts",
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-full" }]],
  outputDir: "test-results-full",
  use: {
    baseURL: "http://localhost:13100",
    locale: "pl-PL",
    timezoneId: "Europe/Warsaw",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "desktop", use: { ...devices["Desktop Chrome"], channel: "chrome" } }],
})
