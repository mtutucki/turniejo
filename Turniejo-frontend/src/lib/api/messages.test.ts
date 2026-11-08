import { describe, expect, it } from "vitest"

import { translateApiMessage } from "./messages"

// QA v3: DRU-020, PUC-017 - komunikaty z API trafiały do UI po angielsku.
describe("translateApiMessage", () => {
  it("DRU-020: tłumaczy duplikat opiekuna", () => {
    expect(translateApiMessage("User is already a guardian of this player")).toBe(
      "Ta osoba jest już opiekunem tego zawodnika",
    )
  })

  it("PUC-017: tłumaczy podwójny wolny los", () => {
    expect(
      translateApiMessage("A pairing cannot have Bye on both sides — there would be no team to advance."),
    ).toBe("Para nie może mieć wolnego losu po obu stronach - nikt by z niej nie awansował")
  })

  it("nieznany komunikat przechodzi bez zmian", () => {
    expect(translateApiMessage("Something new")).toBe("Something new")
  })
})
