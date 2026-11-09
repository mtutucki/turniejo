import { type Browser, expect, type Page, test } from "@playwright/test"

import { type ApiClient, PASSWORD } from "./api"

/** Logowanie w aplikacji (prawdziwy Keycloak za formularzem). */
export async function loginAs(page: Page, client: ApiClient) {
  await page.goto("/login")
  await page.getByLabel("Email").fill(client.email)
  await page.getByLabel("Hasło").fill(PASSWORD)
  await page.getByRole("button", { name: "Zaloguj się" }).click()
  await page.waitForURL((url) => !url.pathname.startsWith("/login"))
}

/** Osobne okno (kontekst przeglądarki) - druga osoba albo drugie urządzenie. */
export async function windowFor(browser: Browser, client?: ApiClient) {
  const context = await browser.newContext()
  const page = await context.newPage()
  if (client) await loginAs(page, client)
  return page
}

/** Strona nie przeładowała się od wejścia (zmiana przyszła na żywo, nie z odświeżenia). */
export async function expectNoReload(page: Page) {
  expect(await page.evaluate(() => performance.getEntriesByType("navigation").length)).toBe(1)
}

export { expect, test }
