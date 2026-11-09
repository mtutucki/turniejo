import releases from "../src/features/whats-new/releases.json"
import { expect, loginAs, mock, ORGANIZER, test } from "./fixtures"

// Okno „Co nowego” po wydaniu (docs/decyzje.md): jedno okno na wszystkie pominięte wersje, zapis na koncie.
const LATEST = releases[0]!.version
const seenBy = async (email: string) =>
  (await mock.state()).users.find((u) => u.email === email)?.seenReleaseVersion

test("[NOWOSCI-01] konto sprzed wydania dostaje jedno okno, które po zamknięciu nie wraca", async ({ page }) => {
  await mock.seenRelease(ORGANIZER, "0.9.0")
  await loginAs(page, ORGANIZER)

  const dialog = page.getByRole("dialog", { name: "Co nowego w Turniejo" })
  await expect(dialog).toBeVisible()
  await expect(page.getByRole("dialog")).toHaveCount(1)
  await dialog.getByRole("button", { name: "Rozumiem" }).click()
  await expect(dialog).toBeHidden()
  await expect.poll(() => seenBy(ORGANIZER)).toBe(LATEST)

  await page.reload()
  await expect(page.getByRole("navigation").first()).toBeVisible()
  await expect(dialog).toBeHidden()
})

test("[NOWOSCI-02] nowe konto nie dostaje okna, a bieżąca wersja zapisuje się sama", async ({ page }) => {
  await loginAs(page, ORGANIZER)

  await expect.poll(() => seenBy(ORGANIZER)).toBe(LATEST)
  await expect(page.getByRole("dialog")).toHaveCount(0)
})

test("[NOWOSCI-03] z okna można przejść do pełnej listy zmian", async ({ page }) => {
  await mock.seenRelease(ORGANIZER, "0.9.0")
  await loginAs(page, ORGANIZER)

  await page.getByRole("dialog").getByRole("link", { name: "Zobacz wszystkie zmiany" }).click()

  await expect(page).toHaveURL(/\/changelog$/)
  await expect(page.getByRole("heading", { name: `Wersja ${LATEST}` })).toBeVisible()
  await expect(page.getByRole("dialog")).toHaveCount(0)
  await expect.poll(() => seenBy(ORGANIZER)).toBe(LATEST)
})
