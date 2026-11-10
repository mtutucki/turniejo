import {
  expect,
  loginAs,
  MOCK_URL,
  mock,
  ORGANIZER,
  PUBLIC_SLUG,
  test,
  TOURNAMENT_ID,
} from "./fixtures"
import { IDS } from "./mock/db"

// Format „Dwie fazy grupowe”: w seedzie Orły i Lwy grają w Grupie A kategorii U12.
test.beforeEach(async () => {
  await mock.tournament(TOURNAMENT_ID, { format: "TwoGroupStages", status: "RegistrationClosed" })
})

async function addSecondStageGroup(page: import("@playwright/test").Page, name: string) {
  const section = page.getByRole("region", { name: "Faza 2 w kategorii U12" })
  await section.getByRole("button", { name: "Dodaj grupę" }).click()
  const dialog = page.getByRole("dialog", { name: "Nowa grupa drugiej fazy · U12" })
  await expect(dialog.getByLabel("Nazwa")).toHaveValue(name)
  await dialog.getByRole("button", { name: "Zapisz" }).click()
  await expect(dialog).toBeHidden()
}

test("[FAZ-016] organizator dodaje grupy drugiej fazy, dostaje gotowy podział i przestawia miejsce", async ({
  page,
}) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
  const section = page.getByRole("region", { name: "Faza 2 w kategorii U12" })
  await expect(section).toContainText("Dodaj grupy drugiej fazy")
  await expect(page.getByRole("region", { name: "Grupy w kategorii U12" })).toContainText("Faza 1")

  await addSecondStageGroup(page, "Grupa I")
  await addSecondStageGroup(page, "Grupa II")

  await section.getByRole("button", { name: "Ustaw podział" }).click()
  const dialog = page.getByRole("dialog", { name: "Podział na drugą fazę · U12" })
  // Gotowy podział: 1. miejsce do pierwszej grupy, 2. miejsce do drugiej.
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 1. miejsce Grupa A" })).toHaveText(
    "Grupa I",
  )
  const second = dialog.getByRole("combobox", { name: "Grupa dla: 2. miejsce Grupa A" })
  await expect(second).toHaveText("Grupa II")

  await second.click()
  await page.getByRole("option", { name: "Grupa I", exact: true }).click()
  await dialog.getByRole("button", { name: "Zapisz podział" }).click()
  await expect(dialog).toBeHidden()

  const groups = section.getByRole("listitem")
  await expect(groups.filter({ hasText: "Grupa I" }).first()).toContainText(
    "1. miejsce Grupa A, 2. miejsce Grupa A",
  )
  await expect(groups.filter({ hasText: "Grupa II" })).toContainText("Bez miejsc")
  expect(await mock.calls("PUT", "/second-stage/seats")).toBe(1)
})

test("[FAZ-017] generowanie terminarza czeka na podział na drugą fazę", async ({ page }) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule/generate`)

  await expect(page.getByRole("listitem").filter({ hasText: "Podział na drugą fazę" })).toBeVisible()
  await expect(
    page.getByText("Każda kategoria potrzebuje grup drugiej fazy z ustawionym podziałem."),
  ).toBeVisible()

  await page.getByRole("link", { name: "Przejdź do Kategorii" }).click()
  await addSecondStageGroup(page, "Grupa I")
  await page.getByRole("button", { name: "Ustaw podział" }).click()
  await page.getByRole("button", { name: "Zapisz podział" }).click()

  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule/generate`)
  await expect(page.getByText("Podział na drugą fazę gotowy")).toBeVisible()
})

/** Grupa I (faza 2): miejsca 1-2 z Grupy A, jeden zaplanowany mecz między nimi. */
async function plannedSecondStage(categoryId: string, groupAId: string, pitchId: string) {
  const plannedAt = new Date(Date.now() + 3 * 60 * 60 * 1000).toISOString()
  const seat = (id: string, position: number) => ({
    id,
    categoryId,
    groupId: "group-i",
    order: position,
    sourceGroupId: groupAId,
    sourcePosition: position,
    tournamentTeamId: null,
  })
  return {
    seats: [seat("seat-1", 1), seat("seat-2", 2)],
    fixtures: [
      {
        id: "fixture-1",
        categoryId,
        groupId: "group-i",
        homeSeatId: "seat-1",
        awaySeatId: "seat-2",
        round: 1,
        plannedPitchId: pitchId,
        plannedAt,
        matchId: null,
      },
    ],
  }
}

test("[FAZ-019] portal pokazuje grupę drugiej fazy z miejscami i zapowiedzią meczu", async ({
  page,
}) => {
  const current = await (await fetch(`${MOCK_URL}/public/tournaments/${PUBLIC_SLUG}`)).json()
  const categoryId = current.categories[0].id
  const groupA = current.groups[0]
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}`,
    body: {
      ...current,
      status: "InProgress",
      format: "TwoGroupStages",
      groups: [groupA, { id: "group-i", categoryId, name: "Grupa I", order: 2, stage: 2 }],
    },
  })
  await mock.override({
    method: "GET",
    path: `/public/tournaments/${PUBLIC_SLUG}/second-stage`,
    body: await plannedSecondStage(categoryId, groupA.id, current.pitches[0].id),
  })

  await page.goto(`/t/${PUBLIC_SLUG}/standings`)
  await expect(page.getByText("Faza 2", { exact: true })).toBeVisible()
  const table = page.getByRole("region", { name: "Tabela: Grupa I" })
  await expect(table).toContainText("Czekają na koniec pierwszej fazy")
  await expect(table).toContainText("1. miejsce Grupa A")
  await expect(table).toContainText("2. miejsce Grupa A")

  await page.goto(`/t/${PUBLIC_SLUG}/schedule`)
  const planned = page.getByTestId("planned-bracket-match")
  await expect(planned).toContainText("Grupa I · czeka na koniec fazy 1")
  await expect(planned).toContainText("1. miejsce Grupa A")
  await expect(planned).toContainText("2. miejsce Grupa A")
})

test("[FAZ-020] terminarz w panelu zapowiada mecz drugiej fazy bez zmiany terminu i sędziego", async ({
  page,
}) => {
  await mock.tournament(TOURNAMENT_ID, { format: "TwoGroupStages", status: "InProgress" })
  await loginAs(page, ORGANIZER)
  await mock.override({
    method: "GET",
    path: `/api/tournaments/${TOURNAMENT_ID}/groups`,
    body: [
      {
        id: IDS.group,
        tournamentId: TOURNAMENT_ID,
        categoryId: IDS.category,
        name: "Grupa A",
        order: 1,
        stage: 1,
      },
      {
        id: "group-i",
        tournamentId: TOURNAMENT_ID,
        categoryId: IDS.category,
        name: "Grupa I",
        order: 2,
        stage: 2,
      },
    ],
  })
  await mock.override({
    method: "GET",
    path: `/api/tournaments/${TOURNAMENT_ID}/second-stage`,
    body: await plannedSecondStage(IDS.category, IDS.group, IDS.pitch),
  })

  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule`)
  const planned = page.getByTestId("planned-bracket-match")
  await expect(planned).toContainText("1. miejsce Grupa A")
  await expect(planned).toContainText("Czeka na koniec pierwszej fazy")
  await expect(planned).toContainText("Grupa I")
  await expect(planned.getByRole("button", { name: /Zmień termin/ })).toHaveCount(0)
})
