import { screen, waitFor, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { beforeEach, describe, expect, it, vi } from "vitest"

import { renderWithQuery } from "@/test/render"
import type { Tournament, TournamentStatus } from "@/types/tournaments"

import { TournamentStatusStepper } from "./status-stepper"

const request = vi.fn()
vi.mock("@/hooks/use-api", () => ({
  useApi: () => ({ request, isAuthenticated: true, getToken: async () => "token" }),
}))
// Stepper pobiera też podsumowanie turnieju (liczba nierozegranych meczów) — liczymy same zmiany statusu.
const statusChanges = () => request.mock.calls.filter(([path]) => String(path).endsWith("/status"))
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

const tournament = (status: TournamentStatus): Tournament => ({
  id: "t-1",
  name: "Puchar Wiosny",
  slug: "puchar-wiosny",
  status,
  format: "Group",
  startAt: "2026-05-01T08:00:00Z",
  endAt: "2026-05-01T18:00:00Z",
  teamLimit: 8,
  isPaid: false,
  refereeCode: null,
  coachCode: null,
  viewerCode: null,
  description: null,
  isPublic: false,
})

describe("TournamentStatusStepper", () => {
  beforeEach(() => {
    request.mockReset()
    request.mockResolvedValue(undefined)
  })

  it("zaznacza bieżący etap", () => {
    renderWithQuery(<TournamentStatusStepper tournament={tournament("RegistrationClosed")} />)
    const steps = screen.getByRole("list", { name: "Etapy turnieju" })
    const current = within(steps)
      .getAllByRole("listitem")
      .map((li) => li.querySelector("[aria-current=step]"))
      .filter(Boolean)
    expect(current).toHaveLength(1)
    expect(current[0]).toHaveTextContent("Zapisy zamknięte")
  })

  it("bez uprawnień nie pokazuje żadnych akcji", () => {
    renderWithQuery(<TournamentStatusStepper tournament={tournament("Draft")} />)
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
  })

  it("następny etap wymaga potwierdzenia i wysyła PATCH ze statusem", async () => {
    const user = userEvent.setup()
    renderWithQuery(<TournamentStatusStepper tournament={tournament("Draft")} canManage />)

    await user.click(screen.getByRole("button", { name: "Otwórz zapisy" }))
    const dialog = await screen.findByRole("alertdialog")
    expect(dialog).toHaveTextContent("Zapisy otwarte")
    expect(dialog).not.toHaveTextContent("Nie da się cofnąć")
    expect(statusChanges()).toHaveLength(0)

    await user.click(within(dialog).getByRole("button", { name: "Otwórz zapisy" }))
    await waitFor(() =>
      expect(request).toHaveBeenCalledWith("/api/tournaments/t-1/status", {
        method: "PATCH",
        body: { tournamentStatus: 2 },
      }),
    )
  })

  it("anulowanie można odwołać w dialogu", async () => {
    const user = userEvent.setup()
    renderWithQuery(<TournamentStatusStepper tournament={tournament("InProgress")} canManage />)
    await user.click(screen.getByRole("button", { name: "Anuluj turniej" }))
    const dialog = await screen.findByRole("alertdialog")
    await user.click(within(dialog).getByRole("button", { name: "Anuluj" }))
    await waitFor(() => expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument())
    expect(statusChanges()).toHaveLength(0)
  })

  it("zakończenie turnieju ostrzega o meczach, które zostaną anulowane", async () => {
    request.mockImplementation(async (path: string) =>
      path.includes("/dashboard")
        ? { matches: { finished: 4, scheduled: 3, total: 7 } }
        : undefined,
    )
    const user = userEvent.setup()
    renderWithQuery(<TournamentStatusStepper tournament={tournament("InProgress")} canManage />)
    await waitFor(() => expect(request).toHaveBeenCalled())

    await user.click(screen.getByRole("button", { name: "Zakończ turniej" }))
    const dialog = await screen.findByRole("alertdialog")
    await waitFor(() =>
      expect(dialog).toHaveTextContent("3 mecze nie zostały rozegrane i zostaną anulowane"),
    )
  })

  it("terminarz nie jest statusem do kliknięcia — prowadzi do generatora", () => {
    renderWithQuery(
      <TournamentStatusStepper tournament={tournament("RegistrationClosed")} canManage />,
    )
    expect(screen.getByRole("link", { name: "Wygeneruj terminarz" })).toHaveAttribute(
      "href",
      "/tournaments/t-1/schedule/generate",
    )
  })

  it("zamknięte zapisy można otworzyć ponownie", async () => {
    const user = userEvent.setup()
    renderWithQuery(
      <TournamentStatusStepper tournament={tournament("RegistrationClosed")} canManage />,
    )
    await user.click(screen.getByRole("button", { name: "Otwórz zapisy ponownie" }))
    const dialog = await screen.findByRole("alertdialog")
    await user.click(within(dialog).getByRole("button", { name: "Otwórz zapisy ponownie" }))
    await waitFor(() =>
      expect(request).toHaveBeenCalledWith("/api/tournaments/t-1/status", {
        method: "PATCH",
        body: { tournamentStatus: 2 },
      }),
    )
  })

  it("z gotowego harmonogramu cofa wyczyszczenie terminarza, nie zmiana statusu", async () => {
    const user = userEvent.setup()
    renderWithQuery(
      <TournamentStatusStepper tournament={tournament("ScheduleGenerated")} canManage />,
    )
    await user.click(screen.getByRole("button", { name: "Wyczyść terminarz" }))
    const dialog = await screen.findByRole("alertdialog")
    await user.click(within(dialog).getByRole("button", { name: "Wyczyść terminarz" }))
    await waitFor(() =>
      expect(request).toHaveBeenCalledWith("/api/tournaments/t-1/schedule", { method: "DELETE" }),
    )
    expect(statusChanges()).toHaveLength(0)
  })

  it("po rozpoczęciu turnieju etapu nie da się cofnąć", () => {
    renderWithQuery(<TournamentStatusStepper tournament={tournament("InProgress")} canManage />)
    expect(screen.getAllByRole("button").map((b) => b.textContent)).toEqual([
      "Zakończ turniej",
      "Anuluj turniej",
    ])
  })

  it("status końcowy blokuje dalsze zmiany", () => {
    renderWithQuery(<TournamentStatusStepper tournament={tournament("Finished")} canManage />)
    expect(screen.getByText(/Status jest ostateczny/)).toBeInTheDocument()
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
  })
})
