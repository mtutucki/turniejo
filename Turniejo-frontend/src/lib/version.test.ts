import { describe, expect, it } from "vitest"

import { formatVersion } from "./version"

describe("formatVersion", () => {
  it("wydanie: sam numer", () => {
    expect(formatVersion("v1.2.0")).toBe("1.2.0")
  })

  it("commity po wydaniu: numer, liczba commitów i skrót", () => {
    expect(formatVersion("v1.2.0-3-gabc1234def")).toBe("1.2.0+3 (abc1234)")
  })

  it("repozytorium bez tagów: skrót commita", () => {
    expect(formatVersion("abc1234def5678")).toBe("abc1234")
  })

  it("lokalnie albo nieznany format: nic", () => {
    expect(formatVersion(undefined)).toBeNull()
    expect(formatVersion("dev")).toBeNull()
    expect(formatVersion("cokolwiek")).toBeNull()
  })
})
