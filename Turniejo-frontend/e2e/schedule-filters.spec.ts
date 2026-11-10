import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

// Dwie kategorie, każda z Grupą A i Grupą B: nazwy grup i drużyn powtarzają się między kategoriami.
const id = (n: number) => `0f000000-0000-4000-8000-0000000c${String(n).padStart(4, "0")}`
const U10 = id(1)
const U12 = id(2)
const categories = [
  { id: U10, tournamentId: TOURNAMENT_ID, name: "U10", minBirthDate: null, maxBirthDate: null, order: 1 },
  { id: U12, tournamentId: TOURNAMENT_ID, name: "U12", minBirthDate: null, maxBirthDate: null, order: 2 },
]
const group = (n: number, categoryId: string, name: string, order: number) => ({
  id: id(n),
  tournamentId: TOURNAMENT_ID,
  categoryId,
  name,
  order,
})
const groups = [
  group(10, U10, "Grupa A", 1),
  group(11, U10, "Grupa B", 2),
  group(12, U12, "Grupa A", 1),
  group(13, U12, "Grupa B", 2),
]
const team = (n: number, categoryId: string, groupId: string, teamName: string, baseTeam = n) => ({
  id: id(n),
  tournamentId: TOURNAMENT_ID,
  teamId: id(baseTeam + 100),
  categoryId,
  groupId,
  registrationStatus: "Accepted",
  paymentStatus: "Paid",
  seedNumber: null,
  teamName,
  teamShortName: null,
  teamLogoUrl: null,
})
const teams = [
  team(20, U10, id(11), "Orły"),
  team(21, U10, id(11), "Lwy"),
  // Ta sama drużyna z bazy w obu kategoriach.
  team(22, U12, id(13), "Orły", 20),
  team(23, U12, id(13), "Żubry"),
  team(24, U10, id(10), "Sokoły"),
  team(25, U10, id(10), "Wilki"),
]
const match = (n: number, categoryId: string, groupId: string, home: number, away: number, hour: number) => ({
  id: id(n),
  tournamentId: TOURNAMENT_ID,
  categoryId,
  groupId,
  pitchId: null,
  homeTournamentTeamId: id(home),
  awayTournamentTeamId: id(away),
  refereeId: null,
  scheduledAt: `2026-12-05T${String(hour).padStart(2, "0")}:00:00Z`,
  status: "Scheduled",
  stageType: "Group",
  homeScore: 0,
  awayScore: 0,
  resultType: "NotPlayed",
  homePenaltyScore: null,
  awayPenaltyScore: null,
  awaitingDecision: false,
})
const schedule = [
  match(30, U10, id(11), 20, 21, 9),
  match(31, U12, id(13), 22, 23, 10),
  match(32, U10, id(10), 24, 25, 11),
]

test.beforeEach(async ({ page }) => {
  await mock.override({ method: "GET", path: "/api/tournaments/:id/categories", body: categories })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/groups", body: groups })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/teams", body: teams })
  await mock.override({ method: "GET", path: "/api/tournaments/:id/schedule", body: schedule })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
  await expect(page.getByRole("heading", { name: "Terminarz" })).toBeVisible()
})

const filters = (page: import("@playwright/test").Page) =>
  page.getByRole("search", { name: "Filtry terminarza" })
const rows = (page: import("@playwright/test").Page) =>
  page.getByRole("listitem").filter({ hasText: " vs " })

test("filtr drużyny pokazuje jej mecze ze wszystkich kategorii i widać go nad listą", async ({ page }) => {
  await filters(page).getByRole("combobox", { name: "Drużyna" }).click()
  // Drużyna zgłoszona do dwóch kategorii jest na liście raz, bez nazwy kategorii.
  await expect(page.getByRole("option", { name: /Orły/ })).toHaveText(["Orły"])
  await page.getByRole("option", { name: "Orły" }).click()

  await expect(rows(page)).toHaveCount(2)
  await expect(page.getByRole("status")).toHaveText("Znaleziono 2 mecze")

  await filters(page).getByRole("combobox", { name: "Kategoria" }).click()
  await page.getByRole("option", { name: "U12" }).click()
  await expect(rows(page)).toHaveCount(1)
  await expect(rows(page)).toContainText("Żubry")

  await page.getByRole("button", { name: "Usuń filtr Orły" }).click()
  await page.getByRole("button", { name: "Usuń filtr U12" }).click()
  await expect(rows(page)).toHaveCount(3)
  await expect(page.getByRole("status")).toHaveCount(0)
})

test("grupy z różnych kategorii da się odróżnić, a filtry wyczyścić naraz", async ({ page }) => {
  await filters(page).getByRole("combobox", { name: "Grupa" }).click()
  await expect(page.getByRole("option", { name: /Grupa B/ })).toHaveText(["U10 · Grupa B", "U12 · Grupa B"])
  await page.getByRole("option", { name: "U10 · Grupa B" }).click()
  await expect(rows(page)).toHaveCount(1)
  await expect(rows(page)).toContainText("Lwy")

  // Po wybraniu kategorii grupy mają już same nazwy.
  await filters(page).getByRole("combobox", { name: "Kategoria" }).click()
  await page.getByRole("option", { name: "U12" }).click()
  await filters(page).getByRole("combobox", { name: "Grupa" }).click()
  await page.getByRole("option", { name: "Grupa B", exact: true }).click()
  await expect(page.getByRole("button", { name: "Usuń filtr U12" })).toBeVisible()
  await expect(page.getByRole("button", { name: "Usuń filtr Grupa B" })).toBeVisible()
  await expect(rows(page)).toHaveCount(1)
  await expect(rows(page)).toContainText("Żubry")

  await page.getByRole("button", { name: "Wyczyść wszystko" }).click()
  await expect(rows(page)).toHaveCount(3)
})
