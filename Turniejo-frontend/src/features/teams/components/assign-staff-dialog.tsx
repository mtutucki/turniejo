"use client"

import { type ReactNode, useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { isApiError } from "@/lib/api/errors"
import {
  JOIN_ROLE_LABELS,
  TEAM_STAFF_ROLE_LABELS,
  type TeamStaffMember,
  type TeamStaffRole,
} from "@/types/teams"

import { useAssignStaff, useTournamentMembers } from "../api"

const ROLES = Object.keys(TEAM_STAFF_ROLE_LABELS) as TeamStaffRole[]

type AssignStaffDialogProps = {
  tournamentId: string
  tournamentTeamId: string
  staff: TeamStaffMember[]
  trigger: ReactNode
}

/**
 * Staff wybiera się spośród osób, które już są w turnieju (np. trener dołączył kodem trenera) —
 * backend nie udostępnia wyszukiwania dowolnych kont.
 */
export function AssignStaffDialog({
  tournamentId,
  tournamentTeamId,
  staff,
  trigger,
}: AssignStaffDialogProps) {
  const [open, setOpen] = useState(false)
  const [userId, setUserId] = useState<string>()
  const [role, setRole] = useState<TeamStaffRole>("Coach")
  const [error, setError] = useState<string>()
  const members = useTournamentMembers(tournamentId, open)
  const assignStaff = useAssignStaff(tournamentId, tournamentTeamId)

  const candidates = (members.data ?? []).filter(
    (m) => !staff.some((s) => s.userId === m.userId && s.role === role),
  )

  function submit() {
    if (!userId) return
    assignStaff.mutate(
      { userId, role },
      {
        onSuccess: () => {
          toast.success(`Przypisano: ${TEAM_STAFF_ROLE_LABELS[role]}`)
          setOpen(false)
        },
        onError: (e) =>
          setError(isApiError(e) ? (e.fieldErrors[0]?.message ?? e.detail) : undefined),
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setUserId(undefined)
          setRole("Coach")
          setError(undefined)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Przypisz osobę do drużyny</DialogTitle>
          <DialogDescription>
            Lista zawiera osoby, które dołączyły do turnieju. Trener dołącza kodem trenera z panelu
            turnieju, a potem przypisujesz go tutaj.
          </DialogDescription>
        </DialogHeader>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="staff-role">Funkcja</FieldLabel>
            <Select value={role} onValueChange={(v) => setRole(v as TeamStaffRole)}>
              <SelectTrigger id="staff-role" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ROLES.map((r) => (
                  <SelectItem key={r} value={r}>
                    {TEAM_STAFF_ROLE_LABELS[r]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="staff-user">Osoba</FieldLabel>
            <Select value={userId} onValueChange={setUserId} disabled={members.isPending}>
              <SelectTrigger id="staff-user" className="w-full">
                <SelectValue placeholder={members.isPending ? "Wczytywanie…" : "Wybierz osobę"} />
              </SelectTrigger>
              <SelectContent>
                {candidates.map((m) => (
                  <SelectItem key={m.userId} value={m.userId}>
                    {m.firstName} {m.lastName} · {m.email}
                    {m.roles.length > 0 &&
                      ` (${m.roles.map((r) => JOIN_ROLE_LABELS[r] ?? r).join(", ")})`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {members.data && candidates.length === 0 && (
              <p className="text-muted-foreground text-sm">
                Brak osób do przypisania w tej funkcji.
              </p>
            )}
          </Field>
          {error && <FieldError>{error}</FieldError>}
        </FieldGroup>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={submit} disabled={!userId || assignStaff.isPending}>
            {assignStaff.isPending ? "Przypisywanie…" : "Przypisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
