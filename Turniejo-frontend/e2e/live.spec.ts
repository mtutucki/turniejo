import { expect, loginAs, MATCH_ID, mock, REFEREE, test } from "./fixtures"

const timer = (page: import("@playwright/test").Page) => page.getByRole("timer")

test("[MNZ-042] panel meczu wstrzymanego przez system: prośba o rozstrzygnięcie zamiast zegara", async ({
  page,
}) => {
  await mock.awaitingDecision(MATCH_ID)
  await loginAs(page, REFEREE)
  await page.goto(`/matches/${MATCH_ID}/live`)

  const board = page.getByRole("region", { name: "Wynik" })
  await expect(board).toContainText("Czeka na rozstrzygnięcie")
  await expect(timer(page)).toHaveCount(0)
  await expect(page.getByRole("button", { name: "WZNÓW" })).toBeVisible()
})

test.describe("Mecz na żywo: sterowanie", () => {
  test.beforeEach(async ({ page }) => {
    await mock.matchStatus(MATCH_ID, "Scheduled")
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
    await expect(page.getByRole("button", { name: "START" })).toBeEnabled()
  })

  test("[MNZ-002 MNZ-003] start uruchamia zegar, pauza go zatrzymuje", async ({ page }) => {
    await expect(timer(page)).toHaveAccessibleName("Czas meczu 00:00")

    await page.getByRole("button", { name: "START" }).click()
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeVisible()
    await expect(timer(page)).not.toHaveAccessibleName("Czas meczu 00:00", { timeout: 5_000 })

    await page.getByRole("button", { name: "PAUZA" }).click()
    await expect(page.getByRole("button", { name: "WZNÓW" })).toBeVisible()
    const paused = await timer(page).getAttribute("aria-label")
    await page.waitForTimeout(2_000)
    await expect(timer(page)).toHaveAttribute("aria-label", paused!)
  })

  test("[MNZ-005] podwójne kliknięcie START i KONIEC wysyła jedno żądanie", async ({ page }) => {
    await page.getByRole("button", { name: "START" }).dblclick()
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeVisible()
    expect(await mock.calls("POST", "/start")).toBe(1)

    await page.getByRole("button", { name: "KONIEC" }).click()
    const dialog = page.getByRole("dialog")
    await dialog.getByRole("button", { name: "Zakończ mecz" }).dblclick()
    await expect(dialog).toBeHidden()
    expect(await mock.calls("POST", "/finish")).toBe(1)
    await expect(page.getByRole("button", { name: "PAUZA" })).toBeHidden()
  })
})

test.describe("Koniec meczu: walidacja rozstrzygnięcia", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
    await page.getByRole("button", { name: "KONIEC" }).click()
  })

  const dialog = (page: import("@playwright/test").Page) =>
    page.getByRole("dialog", { name: "Zakończyć mecz?" })

  test("[PUC-040b] puste pola karnych", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Karne" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Wpisz wynik rzutów karnych")).toBeVisible()
    expect(await mock.calls("POST", "/finish")).toBe(0)
  })

  test("[PUC-040a] karne zakończone remisem", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Karne" }).click()
    await dialog(page).getByLabel("Karne: Orły E2E").fill("4")
    await dialog(page).getByLabel("Karne: Lwy E2E").fill("4")
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Karne muszą wyłonić zwycięzcę")).toBeVisible()
    expect(await mock.calls("POST", "/finish")).toBe(0)
  })

  test("[PUC-043] walkower bez wskazania zwycięzcy", async ({ page }) => {
    await dialog(page).getByRole("button", { name: "Walkower" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page).getByText("Wybierz drużynę, która wygrywa walkowerem")).toBeVisible()

    await dialog(page).getByRole("button", { name: "LWY" }).click()
    await dialog(page).getByRole("button", { name: "Zakończ mecz" }).click()
    await expect(dialog(page)).toBeHidden()
    expect(await mock.calls("POST", "/finish")).toBe(1)
    // Zakończony walkowerem mecz nie wygląda jak zwykły wynik.
    await expect(page.getByTestId("result-note")).toHaveText("walkower")
  })
})

test.describe("Zdarzenia meczu", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, REFEREE)
    await page.goto(`/matches/${MATCH_ID}/live`)
  })

  const events = (page: import("@playwright/test").Page) =>
    page.getByRole("list", { name: "Zdarzenia meczu" })

  async function choosePlayer(
    page: import("@playwright/test").Page,
    dialogName: string,
    player: RegExp,
  ) {
    const dialog = page.getByRole("dialog", { name: dialogName })
    await dialog.getByRole("combobox", { name: "Zawodnik" }).click()
    await page.getByRole("option", { name: player }).click()
    return dialog
  }

  test("[MNZ-012] druga żółta kartka daje czerwoną", async ({ page }) => {
    for (const minute of ["10", "20"]) {
      await page.getByRole("button", { name: "KARTKA", exact: true }).click()
      const dialog = await choosePlayer(page, "Kartka", /Adam Bramka/)
      await dialog.getByLabel("Minuta").fill(minute)
      await dialog.getByRole("button", { name: "Zapisz kartkę" }).click()
      await expect(dialog).toBeHidden()
    }

    await expect(events(page).getByRole("listitem").filter({ hasText: /Żółta/ })).toHaveCount(2)
    await expect(
      events(page)
        .getByRole("listitem")
        .filter({ hasText: /Czerwona/ }),
    ).toHaveCount(1)
  })

  test("[MNZ-017a] edycja gola: strzelec i minuta", async ({ page }) => {
    await page.getByRole("button", { name: "Szybki gol: ORŁ" }).click()
    await page.getByRole("button", { name: "Strzelec: Gol 10'" }).click()
    const dialog = page.getByRole("dialog")
    await dialog.getByRole("combobox").first().click()
    await page.getByRole("option", { name: /Adam Bramka/ }).click()
    await dialog.getByLabel("Minuta").fill("12")
    await dialog.getByRole("button", { name: /Zapisz/ }).click()

    await expect(dialog).toBeHidden()
    await expect(events(page).getByRole("listitem").first()).toContainText("12'")
    await expect(events(page).getByRole("listitem").first()).toContainText("Adam")
  })

  test("[MNZ-019] wybór MVP meczu", async ({ page }) => {
    await page
      .getByRole("region", { name: "MVP meczu" })
      .getByRole("button", { name: "Wybierz" })
      .click()
    const dialog = await choosePlayer(page, "MVP meczu", /Adam Bramka/)
    await dialog.getByRole("button", { name: "Zapisz MVP" }).click()

    await expect(dialog).toBeHidden()
    await expect(page.getByRole("region", { name: "MVP meczu" })).toContainText("Adam")
  })
})

test("[KON-036] sędzia po oknie poprawek nie zmienia rozstrzygnięcia", async ({ page }) => {
  await mock.matchStatus(MATCH_ID, "Finished", new Date(Date.now() - 60_000).toISOString())
  await loginAs(page, REFEREE)
  await page.goto(`/matches/${MATCH_ID}/live`)

  await expect(page.getByText("Mecz zakończony - wynik jest zablokowany.")).toBeVisible()
  await expect(page.getByRole("button", { name: /Zmień rozstrzygnięcie/ })).toHaveCount(0)
})

test("[MNZ-026] panel sędziego na telefonie: wynik widoczny, duże przyciski, dialog w ekranie", async ({
  page,
}, testInfo) => {
  test.skip(testInfo.project.name !== "mobile", "sprawdzenie układu na telefonie")
  await loginAs(page, REFEREE)
  await page.goto(`/matches/${MATCH_ID}/live`)
  const viewport = page.viewportSize()!

  // Brak przewijania w bok.
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
    viewport.width,
  )
  // Wynik widoczny bez przewijania.
  await expect(page.getByRole("region", { name: "Wynik" })).toBeInViewport()
  // Główne przyciski wygodne do kliknięcia palcem (co najmniej 44 px wysokości).
  for (const name of ["PAUZA", "KONIEC", "GOL", "KARTKA"]) {
    const box = (await page.getByRole("button", { name, exact: true }).boundingBox())!
    expect(box.height, name).toBeGreaterThanOrEqual(44)
  }

  for (const [button, dialogName] of [
    ["KONIEC", "Zakończyć mecz?"],
    ["KARTKA", "Kartka"],
  ] as const) {
    await page.getByRole("button", { name: button, exact: true }).click()
    const box = (await page.getByRole("dialog", { name: dialogName }).boundingBox())!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width)
    expect(box.y + box.height).toBeLessThanOrEqual(viewport.height)
    await page.keyboard.press("Escape")
    await expect(page.getByRole("dialog")).toBeHidden()
  }
})
