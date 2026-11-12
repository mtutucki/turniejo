import { expect, loginAs, mock, ORGANIZER, test, TOURNAMENT_ID } from "./fixtures"

const referee = (userId: string, firstName: string, lastName: string) => ({
  userId,
  email: `${firstName.toLowerCase()}@e2e.pl`,
  firstName,
  lastName,
  roles: ["Referee"],
})

test.describe("Sędzia boiska", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ORGANIZER)
  })

  test("organizator wybiera sędziego boiska i widzi go na liście boisk", async ({ page }) => {
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/members",
      body: [referee("0f000000-0000-4000-8000-0000000000a1", "Jan", "Gwizdek")],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/pitches`)
    await page.getByRole("button", { name: "Edytuj: Boisko Główne" }).click()

    const dialog = page.getByRole("dialog")
    await dialog.getByRole("combobox", { name: "Sędzia boiska" }).click()
    await page.getByRole("option", { name: "Jan Gwizdek" }).click()
    await dialog.getByRole("button", { name: "Zapisz" }).click()

    await expect(dialog).toBeHidden()
    await expect(page.getByText("Sędzia: Jan Gwizdek")).toBeVisible()
  })

  test("sędziego innego boiska nie da się wybrać", async ({ page }) => {
    const busy = "0f000000-0000-4000-8000-0000000000a2"
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/members",
      body: [
        referee(busy, "Anna", "Zajęta"),
        referee("0f000000-0000-4000-8000-0000000000a3", "Ola", "Wolna"),
      ],
    })
    await mock.override({
      method: "GET",
      path: "/api/tournaments/:id/pitches",
      body: [
        {
          id: "0f000000-0000-4000-8000-000000000041",
          tournamentId: TOURNAMENT_ID,
          name: "Boisko A",
          description: null,
          isActive: true,
          groupIds: [],
          refereeId: busy,
        },
        {
          id: "0f000000-0000-4000-8000-000000000042",
          tournamentId: TOURNAMENT_ID,
          name: "Boisko B",
          description: null,
          isActive: true,
          groupIds: [],
          refereeId: null,
        },
      ],
    })
    await page.goto(`/tournaments/${TOURNAMENT_ID}/pitches`)
    await expect(page.getByText("Sędzia: Anna Zajęta")).toBeVisible()
    await page.getByRole("button", { name: "Edytuj: Boisko B" }).click()

    await page.getByRole("dialog").getByRole("combobox", { name: "Sędzia boiska" }).click()

    await expect(page.getByRole("option", { name: "Anna Zajęta (Boisko A)" })).toHaveAttribute(
      "aria-disabled",
      "true",
    )
    await expect(page.getByRole("option", { name: "Ola Wolna" })).not.toHaveAttribute(
      "aria-disabled",
      "true",
    )
  })
})
