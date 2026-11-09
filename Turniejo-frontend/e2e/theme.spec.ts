import type { Page } from "@playwright/test"

import { expect, loginAs, MATCH_ID, mock, ORGANIZER, PUBLIC_SLUG, REFEREE, test } from "./fixtures"

// Jasny i ciemny motyw (docs/decyzje.md): domyślnie jak w systemie, przełącznik na wierzchu nagłówka,
// wybór kibica w przeglądarce, zalogowanego także na koncie.
const html = (page: Page) => page.locator("html")
const pick = async (page: Page, label: "Jak w systemie" | "Jasny" | "Ciemny") => {
  await page.getByRole("button", { name: "Motyw" }).first().click()
  await page.getByRole("menuitemradio", { name: label }).click()
}
const themeOf = async (email: string) =>
  (await mock.state()).users.find((u) => u.email === email)?.theme

test("[MOTYW-01] bez wyboru motyw jest jak w systemie", async ({ page }) => {
  await page.emulateMedia({ colorScheme: "dark" })
  await page.goto(`/t/${PUBLIC_SLUG}`)
  await expect(html(page)).toHaveClass(/\bdark\b/)

  await page.emulateMedia({ colorScheme: "light" })
  await expect(html(page)).not.toHaveClass(/\bdark\b/)
})

test("[MOTYW-02] kibic bez konta wybiera jasny na portalu i wybór zostaje po odświeżeniu", async ({
  page,
}) => {
  await page.emulateMedia({ colorScheme: "dark" })
  await page.goto(`/t/${PUBLIC_SLUG}`)

  await pick(page, "Jasny")
  await expect(html(page)).not.toHaveClass(/\bdark\b/)

  await page.reload()
  await expect(html(page)).not.toHaveClass(/\bdark\b/)
  // Nagłówek z przełącznikiem nie rozpycha strony w bok na telefonie.
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - window.innerWidth,
  )
  expect(overflow).toBeLessThanOrEqual(0)
})

test("[MOTYW-03] zalogowany wybiera motyw w nagłówku panelu i zapisuje się on na koncie", async ({
  page,
}) => {
  await page.emulateMedia({ colorScheme: "light" })
  await loginAs(page, ORGANIZER)

  await pick(page, "Ciemny")

  await expect(html(page)).toHaveClass(/\bdark\b/)
  await expect.poll(() => themeOf(ORGANIZER)).toBe("dark")
})

test("[MOTYW-04] motyw z konta działa po zalogowaniu na innym urządzeniu", async ({ page }) => {
  await mock.theme(ORGANIZER, "dark")
  await page.emulateMedia({ colorScheme: "light" })

  await loginAs(page, ORGANIZER)

  await expect(html(page)).toHaveClass(/\bdark\b/)
})

test("[MOTYW-05] sędzia przełącza motyw w panelu meczu na żywo", async ({ page }) => {
  await page.emulateMedia({ colorScheme: "dark" })
  await loginAs(page, REFEREE)
  await page.goto(`/matches/${MATCH_ID}/live`)

  await pick(page, "Jasny")

  await expect(html(page)).not.toHaveClass(/\bdark\b/)
  await expect.poll(() => themeOf(REFEREE)).toBe("light")
})
