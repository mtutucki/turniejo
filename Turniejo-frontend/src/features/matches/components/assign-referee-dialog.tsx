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
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { isApiError } from "@/lib/api/errors"
import type { Match } from "@/types/matches"
import type { TournamentMember } from "@/types/teams"

import { useAssignReferee } from "../api"

const NO_REFEREE = "none"

export const memberName = (m: TournamentMember) => `${m.firstName} ${m.lastName}`.trim() || m.email

type AssignRefereeDialogProps = {
  tournamentId: string
  match: Match
  title: string
  /** Członkowie turnieju z rolą Referee (dołączyli kodem sędziego). */
  referees: TournamentMember[]
  refereeCode: string | null
  trigger: ReactNode
}

export function AssignRefereeDialog({
  tournamentId,
  match,
  title,
  referees,
  refereeCode,
  trigger,
}: AssignRefereeDialogProps) {
  const [open, setOpen] = useState(false)
  const [refereeId, setRefereeId] = useState(match.refereeId ?? NO_REFEREE)
  const [error, setError] = useState<string | null>(null)
  const assign = useAssignReferee(tournamentId)

  function save() {
    setError(null)
    assign.mutate(
      { matchId: match.id, refereeId: refereeId === NO_REFEREE ? null : refereeId },
      {
        onSuccess: () => {
          toast.success(
            refereeId === NO_REFEREE ? "Mecz może wziąć dowolny sędzia" : "Sędzia przypisany",
          )
          setOpen(false)
        },
        onError: (e) => {
          if (isApiError(e) && e.fieldErrors.length) setError(e.fieldErrors[0]!.message)
        },
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setRefereeId(match.refereeId ?? NO_REFEREE)
          setError(null)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Sędzia meczu</DialogTitle>
          <DialogDescription>{title}</DialogDescription>
        </DialogHeader>
        <Field data-invalid={!!error}>
          <FieldLabel htmlFor="match-referee">Sędzia</FieldLabel>
          <Select value={refereeId} onValueChange={setRefereeId}>
            <SelectTrigger id="match-referee" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NO_REFEREE}>Dowolny sędzia</SelectItem>
              {referees.map((r) => (
                <SelectItem key={r.userId} value={r.userId}>
                  {memberName(r)} · {r.email}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <FieldDescription>
            Mecz bez wskazanej osoby może wziąć każdy sędzia turnieju; przypisany widzi go tylko
            wybrany sędzia.{" "}
            {referees.length === 0
              ? "Nikt jeszcze nie dołączył jako sędzia. "
              : "Na liście są osoby, które dołączyły do turnieju jako sędziowie. "}
            {refereeCode && (
              <>
                Kod sędziego: <span className="text-foreground font-mono">{refereeCode}</span>
              </>
            )}
          </FieldDescription>
          {error && <p className="text-destructive text-sm">{error}</p>}
        </Field>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={save} disabled={assign.isPending}>
            {assign.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
