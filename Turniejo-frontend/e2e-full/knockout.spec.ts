import { Scenario } from "./api"
import { expect, expectNoReload, test, windowFor } from "./fixtures"

test("[PUC-062 PUC-045 PUC-046] puchar: drabinka na żywo w dwóch oknach, mistrz i zakończenie turnieju", async ({ browser }) => {
  const s = await Scenario.knockout(4)
  await s.start()

  const organizer = await windowFor(browser, s.organizer)
  await organizer.goto(`/tournaments/${s.tournamentId}/bracket`)
  const fan = await windowFor(browser)
  await fan.goto(`/t/${s.slug}/bracket`)
  for (const page of [organizer, fan]) await expect(page.getByText("T1", { exact: true })).toHaveCount(1)

  // Półfinały rozgrywa organizator przez API - oba okna widzą awans bez odświeżania.
  await s.play("T1", "T2", "T1")
  await s.play("T3", "T4", "T3")
  for (const page of [organizer, fan]) {
    await expect(page.getByText("T1", { exact: true })).toHaveCount(2)
    await expect(page.getByText("T3", { exact: true })).toHaveCount(2)
    await expectNoReload(page)
  }

  await s.play("T1", "T3", "T1")
  await fan.goto(`/t/${s.slug}`)
  await expect(fan.getByRole("region", { name: "Zwycięzcy" })).toContainText("T1")
  await expect(organizer.getByRole("status").filter({ hasText: "T1" }).first()).toBeVisible()

  // Zakończenie turnieju: status ostateczny, bez przycisków zmiany etapu.
  await organizer.goto(`/tournaments/${s.tournamentId}/dashboard`)
  await organizer.getByRole("button", { name: "Zakończ turniej" }).click()
  await organizer.getByRole("alertdialog").getByRole("button", { name: "Zakończ turniej" }).click()
  const steps = organizer.getByRole("list", { name: "Etapy turnieju" })
  await expect(steps.locator("[aria-current=step]")).toHaveText("Zakończony")
  await expect(organizer.getByRole("button", { name: /Zakończ turniej|Anuluj turniej/ })).toHaveCount(0)
})
