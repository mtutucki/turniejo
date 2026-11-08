import { describe, expect, it } from "vitest"

import { resultNote } from "./result-note"

// QA v3: PUC-039 (karne w terminarzu), PUC-041 (dogrywka), PUB-005 (walkower).
describe("resultNote", () => {
  const finished = { status: "Finished", homePenaltyScore: null, awayPenaltyScore: null }

  it("PUC-039: pokazuje wynik karnych", () => {
    expect(resultNote({ ...finished, resultType: "Penalties", homePenaltyScore: 4, awayPenaltyScore: 3 })).toBe("karne 4:3")
  })

  it("PUC-041: oznacza wynik po dogrywce", () => {
    expect(resultNote({ ...finished, resultType: "ExtraTime" })).toBe("po dogrywce")
  })

  it("PUB-005: oznacza walkower", () => {
    expect(resultNote({ ...finished, resultType: "Walkover" })).toBe("walkower")
  })

  it("zwykły wynik i mecz w trakcie nie mają dopisku", () => {
    expect(resultNote({ ...finished, resultType: "Regular" })).toBeNull()
    expect(resultNote({ ...finished, status: "Live", resultType: "ExtraTime" })).toBeNull()
  })
})
