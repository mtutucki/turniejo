import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

const CATEGORY = "0f000000-0000-4000-8000-000000000020"
const GROUP_A = "0f000000-0000-4000-8000-000000000030"
const GROUP_B = "0f000000-0000-4000-8000-000000000031"

const group = (id: string, name: string, order: number) => ({
  id,
  tournamentId: TOURNAMENT_ID,
  categoryId: CATEGORY,
  name,
  order,
})

const pitch = (id: string, name: string, groupIds: string[]) => ({
  id,
  tournamentId: TOURNAMENT_ID,
  name,
  description: null,
  isActive: true,
  groupIds,
})

const team = (i: number, groupId: string) => ({
  id: `0f000000-0000-4000-8000-0000000002${String(i).padStart(2, "0")}`,
  tournamentId: TOURNAMENT_ID,
  teamId: `t${i}`,
  categoryId: CATEGORY,
  groupId,
  registrationStatus: "Accepted",
  paymentStatus: "NotRequired",
  seedNumber: null,
  teamName: `Drużyna ${i}`,
  teamShortName: null,
  teamLogoUrl: null,
})

test.describe("Boiska przypisane do grup", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("organizator przypisuje grupę do boiska i widzi ją na liście", async ({ page }) => {
    await page.goto(`/tournaments/${TOURNAMENT_ID}/pitches`)
    await page.getByRole("button", { name: "Edytuj: Boisko Główne" }).click()

    const dialog = page.getByRole("dialog")
    await expect(dialog.getByText("Grupy na tym boisku")).toBeVisible()
    await dialog.getByRole("checkbox", { name: "Grupa A" }).check()
    await dialog.getByRole("button", { name: "Zapisz" }).click()

    await expect(dialog).toBeHidden()
    await expect(page.getByText("Grupy: Grupa A")).toBeVisible()

    // Zapisane: po ponownym otwarciu grupa jest zaznaczona.
    await page.getByRole("button", { name: "Edytuj: Boisko Główne" }).click()
    await expect(dialog.getByRole("checkbox", { name: "Grupa A" })).toBeChecked()
  })

  test("generator nie pozwala zostawić grupy bez jej boiska", async ({ page }) => {
    const start = new Date(Date.now() + 86_400_000)
    start.setUTCHours(8, 0, 0, 0)
    await mock.tournament(TOURNAMENT_ID, {
      status: "RegistrationClosed",
      startAt: start.toISOString(),
      endAt: new Date(start.getTime() + 2 * 86_400_000).toISOString(),
    })
    await mock.override({ method: "GET", path: "/api/tournaments/:id/schedule", body: [] })
    await mock.override({ method: "GET", path: "/api/tournaments/:id/members", body: [] })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/groups",
      body: [group(GROUP_A, "Grupa A", 1), group(GROUP_B, "Grupa B", 2)],
    })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/teams",
      body: [team(1, GROUP_A), team(2, GROUP_A), team(3, GROUP_B), team(4, GROUP_B)],
    })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/pitches",
      body: [
        pitch("0f000000-0000-4000-8000-000000000041", "Boisko A", [GROUP_A]),
        pitch("0f000000-0000-4000-8000-000000000042", "Boisko C", [GROUP_B]),
      ],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule/generate`)

    const pitchC = page.getByRole("checkbox", { name: /Boisko C/ })
    await expect(page.getByText("Boisko A(Grupa A)")).toBeVisible()
    await expect(pitchC).toBeChecked()
    const generate = page.getByRole("button", { name: "Generuj terminarz" })
    await expect(generate).toBeEnabled()

    await pitchC.uncheck()

    await expect(page.getByText("Bez wybranego boiska: Grupa B.", { exact: false })).toBeVisible()
    await expect(generate).toBeDisabled()
  })
})
