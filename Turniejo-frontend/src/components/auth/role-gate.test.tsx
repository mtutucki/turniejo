import { render, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it } from "vitest"

import { useAuthStore } from "@/stores/auth-store"
import { UserRole } from "@/types/roles"

import { RoleGate } from "./role-gate"

const TOURNAMENT = "5a1d3c7e-0000-4000-8000-00000000000a"
const OTHER = "5a1d3c7e-0000-4000-8000-00000000000b"

function gate({ resourceId }: { resourceId: string | undefined } = { resourceId: TOURNAMENT }) {
  return render(
    <RoleGate
      resourceType="Tournament"
      resourceId={resourceId}
      allow={[UserRole.Organizer]}
      fallback={<p>Brak uprawnień</p>}
    >
      <button type="button">Usuń turniej</button>
    </RoleGate>,
  )
}

describe("RoleGate", () => {
  beforeEach(() => useAuthStore.getState().reset())

  it("dopóki role się nie wczytały, pokazuje fallback", () => {
    gate()
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
    expect(screen.getByText("Brak uprawnień")).toBeInTheDocument()
  })

  it("pokazuje treść, gdy user ma rolę w tym zasobie (id bez względu na wielkość liter)", () => {
    useAuthStore
      .getState()
      .setRoleAssignments([
        { role: UserRole.Organizer, scopeType: "Tournament", scopeId: TOURNAMENT.toUpperCase() },
      ])
    gate()
    expect(screen.getByRole("button", { name: "Usuń turniej" })).toBeInTheDocument()
  })

  it("rola w innym zasobie nie wystarcza (dopasowanie typ + id)", () => {
    useAuthStore.getState().setRoleAssignments([
      { role: UserRole.Organizer, scopeType: "Tournament", scopeId: OTHER },
      { role: UserRole.Organizer, scopeType: "Organization", scopeId: TOURNAMENT },
    ])
    gate()
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
  })

  it("inna rola w tym samym zasobie nie wystarcza", () => {
    useAuthStore
      .getState()
      .setRoleAssignments([
        { role: UserRole.Referee, scopeType: "Tournament", scopeId: TOURNAMENT },
      ])
    gate()
    expect(screen.getByText("Brak uprawnień")).toBeInTheDocument()
  })

  it("bez id zasobu niczego nie odsłania", () => {
    useAuthStore
      .getState()
      .setRoleAssignments([
        { role: UserRole.Organizer, scopeType: "Tournament", scopeId: TOURNAMENT },
      ])
    gate({ resourceId: undefined })
    expect(screen.queryByRole("button")).not.toBeInTheDocument()
  })
})
