import { describe, expect, it } from "vitest"

import { tournamentResult, winRate } from "./team-history"

const entry = (patch: Partial<Parameters<typeof tournamentResult>[0]>) => ({
  resultKind: "None" as const,
  eliminatedRoundFromEnd: null,
  groupPosition: null,
  groupName: null,
  ...patch,
})

describe("tournamentResult", () => {
  it("miejsca w pucharze", () => {
    expect(tournamentResult(entry({ resultKind: "Champion" }))).toBe("Zwycięzca")
    expect(tournamentResult(entry({ resultKind: "RunnerUp" }))).toBe("2. miejsce")
    expect(tournamentResult(entry({ resultKind: "Fourth" }))).toBe("4. miejsce")
  })

  it("odpadnięcie z nazwą rundy", () => {
    expect(tournamentResult(entry({ resultKind: "Eliminated", eliminatedRoundFromEnd: 2 }))).toBe(
      "Odpadła w półfinale",
    )
    expect(tournamentResult(entry({ resultKind: "Eliminated", eliminatedRoundFromEnd: 3 }))).toBe(
      "Odpadła w ćwierćfinale",
    )
    expect(tournamentResult(entry({ resultKind: "Eliminated", eliminatedRoundFromEnd: 4 }))).toBe(
      "Odpadła w 1/8 finału",
    )
  })

  it("miejsce w grupie odmienia „Grupa A” na „Grupie A”", () => {
    expect(
      tournamentResult(entry({ resultKind: "Group", groupPosition: 2, groupName: "Grupa A" })),
    ).toBe("2. miejsce w Grupie A")
    expect(
      tournamentResult(entry({ resultKind: "Group", groupPosition: 1, groupName: "Orły" })),
    ).toBe("1. miejsce w grupie „Orły”")
  })
})

describe("winRate", () => {
  it("procent zwycięstw albo brak przy zerze meczów", () => {
    expect(winRate({ played: 3, wins: 2, draws: 0, losses: 1, goalsFor: 0, goalsAgainst: 0 })).toBe(
      67,
    )
    expect(
      winRate({ played: 0, wins: 0, draws: 0, losses: 0, goalsFor: 0, goalsAgainst: 0 }),
    ).toBeNull()
  })
})
