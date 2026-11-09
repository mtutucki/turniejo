import { Scenario } from "./api"
import { expect, expectNoReload, test, windowFor } from "./fixtures"

test("[MNZ-023 PUB-010 MNZ-020 GRP-014] mecz na dwóch urządzeniach, kibic na żywo, poprawka po końcu i tabela", async ({ browser }) => {
  const s = await Scenario.groups(1, 4)
  await s.start()
  const referee = await s.referee()
  const match = await s.match("T1", "T2")
  await s.organizer.patch(`${s.base}/matches/${match.id}/referee`, { refereeId: referee.userId })
  const home = match.homeTournamentTeamId === s.team("T1") ? "T1" : "T2"
  const away = home === "T1" ? "T2" : "T1"

  const phone = await windowFor(browser, referee)
  const tablet = await windowFor(browser, referee)
  const fan = await windowFor(browser)
  for (const page of [phone, tablet]) await page.goto(`/matches/${match.id}/live`)
  await fan.goto(`/t/${s.slug}/matches/${match.id}`)

  await phone.getByRole("button", { name: "START" }).click()
  await expect(tablet.getByRole("button", { name: "PAUZA" })).toBeVisible()

  // Gol z jednego urządzenia, gol z drugiego: wszyscy widzą 1:1, bez zgubionych i podwójnych goli.
  await phone.getByRole("button", { name: `Szybki gol: ${home.toUpperCase()}` }).click()
  await expect(tablet.getByLabel(`${home}: 1`, { exact: true })).toBeVisible()
  await tablet.getByRole("button", { name: `Szybki gol: ${away.toUpperCase()}` }).click()
  for (const page of [phone, tablet]) {
    await expect(page.getByLabel(`${home}: 1`, { exact: true })).toBeVisible()
    await expect(page.getByLabel(`${away}: 1`, { exact: true })).toBeVisible()
  }
  await expect(fan.getByTestId("detail-score")).toHaveText("1:1")
  await expectNoReload(fan)

  // Koniec meczu z informacją o oknie poprawek, potem poprawka: gol gospodarzy po końcu.
  await phone.getByRole("button", { name: "KONIEC" }).click()
  const dialog = phone.getByRole("dialog", { name: "Zakończyć mecz?" })
  await expect(dialog).toContainText(/Po zakończeniu przez \d+ min możesz jeszcze poprawiać zdarzenia, potem wynik jest zablokowany\./)
  await dialog.getByRole("button", { name: "Zakończ mecz" }).click()
  await expect(dialog).toBeHidden()

  await referee.post(`${s.base}/matches/${match.id}/goals`, {
    tournamentTeamId: s.team(home), playerId: null, assistPlayerId: null, goalType: 5, minute: 30, second: null,
  })
  await expect(fan.getByTestId("detail-score")).toHaveText("2:1")

  // Tabela: punkty 3/1/0, mecze, bilans.
  const organizer = await windowFor(browser, s.organizer)
  await organizer.goto(`/tournaments/${s.tournamentId}/standings`)
  const row = organizer.getByRole("row").filter({ hasText: home })
  await expect(row).toContainText("3")
  await expect(row).toContainText("2:1")
})

test("[PAN-016] terminarz odświeża się na żywo w drugim oknie", async ({ browser }) => {
  const s = await Scenario.groups(1, 4)
  await s.start()
  const match = await s.match("T3", "T4")
  const second = await windowFor(browser, s.organizer)
  await second.goto(`/tournaments/${s.tournamentId}/schedule`)
  await expect(second.getByText("T3").first()).toBeVisible()

  const later = new Date(s.startsAt.getTime() + 6 * 3_600_000)
  await s.organizer.patch(`${s.base}/matches/${match.id}/time`, { scheduledAt: later.toISOString() })

  const time = later.toLocaleTimeString("pl-PL", { hour: "2-digit", minute: "2-digit", timeZone: "Europe/Warsaw" })
  await expect(second.getByText(time).first()).toBeVisible()
  await expectNoReload(second)
})
