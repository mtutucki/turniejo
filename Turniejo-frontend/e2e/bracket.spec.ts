import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

type Page = import("@playwright/test").Page

const CATEGORY = "0f000000-0000-4000-8000-000000000020"

/** Puchar po zamknięciu zapisów z n zaakceptowanymi drużynami „Drużyna 1…n” (rozstawienie = numer). */
async function knockoutWith(n: number) {
  await mock.tournament(TOURNAMENT_ID, { format: "Knockout", status: "RegistrationClosed" })
  await mock.override({
    method: "GET",
    path: "/api/tournaments/:id/teams",
    body: Array.from({ length: n }, (_, i) => ({
      id: `0f000000-0000-4000-8000-0000000001${String(i).padStart(2, "0")}`,
      tournamentId: TOURNAMENT_ID,
      teamId: `t${i}`,
      categoryId: CATEGORY,
      groupId: null,
      registrationStatus: "Accepted",
      paymentStatus: "NotRequired",
      seedNumber: i + 1,
      teamName: `Drużyna ${i + 1}`,
      teamShortName: null,
      teamLogoUrl: null,
    })),
  })
}

async function pick(page: Page, slot: string, option: string) {
  await page.getByRole("combobox", { name: slot }).click()
  await page.getByRole("option", { name: option, exact: true }).click()
}

test.describe("Drabinka pucharu", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("[PUC-009 PUC-014] utworzenie drabinki z propozycji i reset przed startem", async ({ page }) => {
    await knockoutWith(8)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/bracket`)
    await expect(page.getByRole("combobox", { name: "Para 1: gość" })).toHaveText(/Drużyna 8/)

    await page.getByRole("button", { name: "Utwórz drabinkę" }).click()
    await expect(page.getByText("Drabinka utworzona")).toBeVisible()
    await expect(page.getByText("Ćwierćfinały").first()).toBeVisible()
    await expect(page.getByText("Półfinały").first()).toBeVisible()
    await expect(page.getByText("Finał").first()).toBeVisible()

    await page.getByRole("button", { name: "Usuń drabinkę" }).click()
    const confirm = page.getByRole("alertdialog", { name: "Usunąć drabinkę?" })
    await confirm.getByRole("button", { name: "Usuń drabinkę" }).click()
    await expect(page.getByText("Drabinka usunięta")).toBeVisible()
    await expect(page.getByRole("button", { name: "Utwórz drabinkę" })).toBeVisible()
  })

  test("[PUC-011] ta sama drużyna dwa razy blokuje utworzenie", async ({ page }) => {
    await knockoutWith(8)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/bracket`)

    await pick(page, "Para 2: gospodarz", "Drużyna 1")

    await expect(page.getByText("To samo miejsce albo drużyna występuje dwa razy.")).toBeVisible()
    await expect(page.getByRole("button", { name: "Utwórz drabinkę" })).toBeDisabled()
  })

  test("[PUC-012] wolny los po obu stronach pary blokuje utworzenie", async ({ page }) => {
    await knockoutWith(8)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/bracket`)

    await pick(page, "Para 1: gospodarz", "Wolny los")
    await pick(page, "Para 1: gość", "Wolny los")

    await expect(page.getByText("Para nie może mieć wolnego losu po obu stronach.")).toBeVisible()
    await expect(page.getByRole("button", { name: "Utwórz drabinkę" })).toBeDisabled()
  })

  test("[PUC-020] ponad 16 drużyn: informacja, ile się zmieści", async ({ page }) => {
    await knockoutWith(17)
    await page.goto(`/tournaments/${TOURNAMENT_ID}/bracket`)

    await expect(page.getByText("Do drabinki zmieści się 16 z 17 drużyn.", { exact: false })).toBeVisible()
  })
})
