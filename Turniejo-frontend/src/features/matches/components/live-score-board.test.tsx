import { render, screen, within } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { describe, expect, it, vi } from "vitest"

import type { MatchTeam } from "@/types/matches"

import { LiveScoreBoard, type LiveScoreBoardProps } from "./live-score-board"

const home: MatchTeam = {
  tournamentTeamId: "tt-home",
  teamId: "t1",
  name: "Orły Warszawa",
  shortName: "ORŁ",
  players: [],
}
const away: MatchTeam = {
  tournamentTeamId: "tt-away",
  teamId: "t2",
  name: "Lwy Kraków",
  shortName: null,
  players: [],
}

function setup(overrides: Partial<LiveScoreBoardProps> = {}) {
  const props: LiveScoreBoardProps = {
    status: "Live",
    homeTeam: home,
    awayTeam: away,
    homeScore: 2,
    awayScore: 1,
    seconds: 754,
    canEdit: true,
    onGoal: vi.fn(),
    onUndo: vi.fn(),
    canUndo: () => true,
    ...overrides,
  }
  const view = render(<LiveScoreBoard {...props} />)
  return { props, ...view }
}

describe("LiveScoreBoard", () => {
  it("pokazuje drużyny, wynik, status i zegar", () => {
    setup()
    const board = screen.getByRole("region", { name: "Wynik" })
    expect(within(board).getByText("Orły Warszawa")).toBeInTheDocument()
    expect(within(board).getByText("Lwy Kraków")).toBeInTheDocument()
    expect(screen.getByLabelText("Orły Warszawa: 2")).toHaveTextContent("2")
    expect(screen.getByLabelText("Lwy Kraków: 1")).toHaveTextContent("1")
    expect(screen.getByRole("timer")).toHaveTextContent("12:34")
  })

  it("szybki gol i cofnięcie wołają callback z właściwą drużyną", async () => {
    const user = userEvent.setup()
    const { props } = setup()
    await user.click(screen.getByRole("button", { name: "Szybki gol: ORŁ" }))
    // Bez skrótu nazwy przycisk używa pełnej nazwy drużyny.
    await user.click(screen.getByRole("button", { name: "Cofnij ostatniego gola: Lwy Kraków" }))
    expect(props.onGoal).toHaveBeenCalledExactlyOnceWith(home)
    expect(props.onUndo).toHaveBeenCalledExactlyOnceWith(away)
  })

  it("blokuje cofnięcie, gdy drużyna nie ma gola", () => {
    setup({ canUndo: (team) => team.tournamentTeamId === "tt-home" })
    expect(screen.getByRole("button", { name: "Cofnij ostatniego gola: ORŁ" })).toBeEnabled()
    expect(
      screen.getByRole("button", { name: "Cofnij ostatniego gola: Lwy Kraków" }),
    ).toBeDisabled()
  })

  it("bez uprawnień nie pokazuje przycisków +/−", () => {
    setup({ canEdit: false })
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
  })

  it("aktualizuje wynik po zmianie (np. gol z SignalR)", async () => {
    const { rerender, props } = setup()
    rerender(<LiveScoreBoard {...props} homeScore={3} />)
    expect(await screen.findByLabelText("Orły Warszawa: 3")).toHaveTextContent(/^3$/)
  })

  it("pokazuje rzuty karne tylko gdy są oba wyniki", () => {
    const { rerender, props } = setup({ status: "Finished", homeScore: 1, awayScore: 1 })
    expect(screen.queryByTestId("penalties")).not.toBeInTheDocument()
    rerender(<LiveScoreBoard {...props} homePenaltyScore={5} awayPenaltyScore={4} />)
    expect(screen.getByTestId("penalties")).toHaveTextContent("karne 5:4")
  })
})
