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

  await section.getByRole("button", { name: "Pojedyncze miejsca" }).click()
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
  const first = groups.filter({ hasText: "Grupa I" }).first()
  await expect(first).toContainText("2 drużyny")
  await expect(first).toContainText("1. miejsce: Grupa A")
  await expect(first).toContainText("2. miejsce: Grupa A")
  await expect(groups.filter({ hasText: "Grupa II" })).toContainText("Bez miejsc")
  expect(await mock.calls("PUT", "/second-stage/seats")).toBe(1)
})

test("[FAZ-021] podział da się ustawić, zanim grupy mają drużyny", async ({ page }) => {
  // Kreator: grupy są, drużyn jeszcze nie.
  await mock.override({ method: "GET", path: `/api/tournaments/${TOURNAMENT_ID}/teams`, body: [] })
  await mock.tournament(TOURNAMENT_ID, { teamLimit: 4 })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
  await addSecondStageGroup(page, "Grupa I")
  await addSecondStageGroup(page, "Grupa II")

  await page.getByRole("button", { name: "Pojedyncze miejsca" }).click()
  const dialog = page.getByRole("dialog", { name: "Podział na drugą fazę · U12" })
  const size = dialog.getByLabel("Drużyn w grupie")
  await expect(size).toHaveValue("4")
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 2. miejsce Grupa A" })).toHaveText(
    "Grupa I",
  )
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 4. miejsce Grupa A" })).toHaveText(
    "Grupa II",
  )

  await size.fill("6")
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 3. miejsce Grupa A" })).toHaveText(
    "Grupa I",
  )
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 6. miejsce Grupa A" })).toHaveText(
    "Grupa II",
  )
  await dialog.getByRole("button", { name: "Zapisz podział" }).click()
  await expect(dialog).toBeHidden()

  await expect(
    page
      .getByRole("region", { name: "Faza 2 w kategorii U12" })
      .getByRole("listitem")
      .filter({ hasText: "Grupa II" }),
  ).toContainText("4. miejsce: Grupa A5. miejsce: Grupa A6. miejsce: Grupa A")
})

test("[FAZ-023] miejsce zapisane ponad liczbę drużyn grupy jest oznaczone i wypada przy zapisie", async ({
  page,
}) => {
  // Podział zapisany na 3 miejsca, zanim zgłosiły się drużyny; w Grupie A grają dwie.
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
  await addSecondStageGroup(page, "Grupa I")
  await mock.override({
    method: "GET",
    path: `/api/tournaments/${TOURNAMENT_ID}/second-stage`,
    times: 1,
    body: {
      seats: [1, 2, 3].map((position) => ({
        id: `seat-${position}`,
        categoryId: IDS.category,
        groupId: "placeholder",
        order: position,
        sourceGroupId: IDS.group,
        sourcePosition: position,
        tournamentTeamId: null,
      })),
      fixtures: [],
    },
  })
  await page.reload()
  const section = page.getByRole("region", { name: "Faza 2 w kategorii U12" })
  await expect(section).toContainText("których nie ma w tabeli: 3. miejsce Grupa A")

  await section.getByRole("button", { name: "Pojedyncze miejsca" }).click()
  const dialog = page.getByRole("dialog", { name: "Podział na drugą fazę · U12" })
  await expect(dialog.getByLabel("Drużyn w grupie")).toHaveCount(0)
  await expect(dialog.getByRole("combobox", { name: /Grupa dla:/ })).toHaveCount(2)
  await dialog.getByRole("button", { name: "Gotowy podział" }).click()
  await dialog.getByRole("button", { name: "Zapisz podział" }).click()
  await expect(dialog).toBeHidden()
  await expect(section).not.toContainText("których nie ma w tabeli")
})

test("[FAZ-024] 8 drużyn bez grup w 4 grupach: podział domyślnie po 2 miejsca w grupie", async ({
  page,
}) => {
  // Kreator: drużyny zgłoszone do kategorii, jeszcze bez grup.
  await mock.override({
    method: "GET",
    path: `/api/tournaments/${TOURNAMENT_ID}/teams`,
    body: Array.from({ length: 8 }, (_, i) => ({
      id: `tt-${i + 1}`,
      tournamentId: TOURNAMENT_ID,
      teamId: `team-${i + 1}`,
      categoryId: IDS.category,
      groupId: null,
      registrationStatus: "Accepted",
      paymentStatus: "NotRequired",
      seedNumber: null,
      teamName: `Drużyna ${i + 1}`,
      teamShortName: null,
      teamLogoUrl: null,
    })),
  })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
  for (const name of ["Grupa B", "Grupa C", "Grupa D"]) {
    await page
      .getByRole("region", { name: "Grupy w kategorii U12" })
      .getByRole("button", { name: "Dodaj grupę" })
      .click()
    const dialog = page.getByRole("dialog", { name: "Nowa grupa · U12" })
    await expect(dialog.getByLabel("Nazwa")).toHaveValue(name)
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()
  }
  await addSecondStageGroup(page, "Grupa I")
  await addSecondStageGroup(page, "Grupa II")

  await page.getByRole("button", { name: "Pojedyncze miejsca" }).click()
  const dialog = page.getByRole("dialog", { name: "Podział na drugą fazę · U12" })
  await expect(dialog.getByLabel("Drużyn w grupie")).toHaveValue("2")
  await expect(dialog.getByText("Razem miejsc: 8 · drużyn w kategorii: 8")).toBeVisible()
  await expect(dialog.getByRole("combobox", { name: /Grupa dla:/ })).toHaveCount(8)
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 1. miejsce Grupa D" })).toHaveText(
    "Grupa I",
  )
  await expect(dialog.getByRole("combobox", { name: "Grupa dla: 2. miejsce Grupa D" })).toHaveText(
    "Grupa II",
  )

  await dialog.getByLabel("Drużyn w grupie").fill("3")
  await expect(
    dialog.getByText("Razem miejsc: 12 · drużyn w kategorii: 8. Miejsc jest więcej niż drużyn."),
  ).toBeVisible()
})

test("[FAZ-030] limit 8 drużyn w 4 grupach: zakresy po jednym miejscu, dalsze miejsca z ostrzeżeniem", async ({
  page,
}) => {
  // Kreator: limit turnieju 8 drużyn, 4 grupy, drużyn jeszcze nie ma - w grupie będą po 2.
  await mock.override({ method: "GET", path: `/api/tournaments/${TOURNAMENT_ID}/teams`, body: [] })
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/categories`)
  for (const name of ["Grupa B", "Grupa C", "Grupa D"]) {
    await page
      .getByRole("region", { name: "Grupy w kategorii U12" })
      .getByRole("button", { name: "Dodaj grupę" })
      .click()
    const dialog = page.getByRole("dialog", { name: "Nowa grupa · U12" })
    await expect(dialog.getByLabel("Nazwa")).toHaveValue(name)
    await dialog.getByRole("button", { name: "Zapisz" }).click()
    await expect(dialog).toBeHidden()
  }
  await addSecondStageGroup(page, "Grupa I")
  await addSecondStageGroup(page, "Grupa II")
  const section = page.getByRole("region", { name: "Faza 2 w kategorii U12" })
  await expect(section.getByRole("listitem").filter({ hasText: "Grupa II" })).toContainText(
    "Domyślnie: Miejsce 2 z każdej grupy",
  )
  await expect(section.getByRole("listitem").filter({ hasText: "Grupa I" }).first()).toContainText(
    "Domyślnie: Miejsce 1 z każdej grupy",
  )

  await section.getByRole("button", { name: "Ustaw podział" }).click()
  const dialog = page.getByRole("dialog", { name: "Podział na drugą fazę · U12" })
  await expect(dialog).toContainText("8 drużyn (limit turnieju) w 4 grupach: po 2 w grupie.")
  await expect(dialog.getByLabel("Grupa I: od miejsca")).toHaveValue("1")
  await expect(dialog.getByLabel("Grupa I: do miejsca")).toHaveValue("1")
  await expect(dialog.getByLabel("Grupa II: od miejsca")).toHaveValue("2")
  await expect(dialog.getByLabel("Grupa II: do miejsca")).toHaveValue("2")

  await dialog.getByLabel("Grupa II: do miejsca").fill("4")
  await expect(dialog).toContainText(
    "Grupy będą miały po 2 drużyny, więc miejsc powyżej 2 nie będzie.",
  )

  await dialog.getByLabel("Grupa II: od miejsca").fill("1")
  await expect(dialog.getByRole("alert")).toHaveText(
    "To samo miejsce nie może trafić do dwóch grup.",
  )
  await expect(dialog.getByRole("button", { name: "Zapisz podział" })).toBeDisabled()

  await dialog.getByRole("button", { name: "Gotowy podział" }).click()
  await dialog.getByRole("button", { name: "Zapisz podział" }).click()
  await expect(dialog).toBeHidden()

  await expect(section.getByRole("listitem").filter({ hasText: "Grupa II" })).toContainText(
    "Miejsce 2 z każdej grupy",
  )
  await expect(section).not.toContainText("Domyślnie")
  expect(await mock.calls("PUT", "/second-stage/ranges")).toBe(1)
})

test("[FAZ-017] generowanie terminarza czeka na grupy drugiej fazy, a bez podziału bierze gotowy", async ({
  page,
}) => {
  await loginAs(page, ORGANIZER)
  await page.goto(`/tournaments/${TOURNAMENT_ID}/schedule/generate`)

  await expect(
    page.getByRole("listitem").filter({ hasText: "Podział na drugą fazę" }),
  ).toBeVisible()
  await expect(page.getByText("Każda kategoria potrzebuje grup drugiej fazy.")).toBeVisible()

  await page.getByRole("link", { name: "Przejdź do Kategorii" }).click()
  await addSecondStageGroup(page, "Grupa I")
  await expect(page.getByRole("region", { name: "Faza 2 w kategorii U12" })).toContainText(
    "Domyślnie: Miejsca 1-2 z każdej grupy",
  )

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
