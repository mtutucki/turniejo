"use client"

import { HeartHandshake } from "lucide-react"
import { useState } from "react"
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
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { useAssignGuardian } from "@/features/panels/api"
import { isApiError } from "@/lib/api/errors"
import { type MyChild, RELATIONSHIP_LABELS } from "@/types/roles-panels"
import type { TournamentPlayer } from "@/types/teams"

import { useTournamentMembers } from "../api"

type Relationship = MyChild["relationshipType"]
const RELATIONSHIPS = Object.keys(RELATIONSHIP_LABELS) as Relationship[]

/**
 * Organizator wskazuje rodzica zawodnika spośród osób, które dołączyły do turnieju (lista członków
 * jest tylko dla organizatora). Duplikat odrzuca backend.
 */
export function AssignGuardianDialog({
  tournamentId,
  tournamentTeamId,
  player,
}: {
  tournamentId: string
  tournamentTeamId: string
  player: TournamentPlayer
}) {
  const [open, setOpen] = useState(false)
  const [userId, setUserId] = useState<string>()
  const [relationship, setRelationship] = useState<Relationship>("Parent")
  const [canEdit, setCanEdit] = useState(false)
  const [error, setError] = useState<string>()
  const members = useTournamentMembers(tournamentId, open)
  const assign = useAssignGuardian(player.playerId)
  const name = `${player.firstName} ${player.lastName}`

  function submit() {
    if (!userId) return
    assign.mutate(
      { tournamentId, tournamentTeamId, userId, relationshipType: relationship, canEdit },
      {
        onSuccess: () => {
          toast.success(`Przypisano opiekuna: ${name}`)
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
          setRelationship("Parent")
          setCanEdit(false)
          setError(undefined)
        }
      }}
    >
      <DialogTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          aria-label={`Dodaj opiekuna: ${name}`}
          title="Dodaj opiekuna"
        >
          <HeartHandshake />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Opiekun zawodnika</DialogTitle>
          <DialogDescription>
            {name} - rodzic zobaczy terminarz, wyniki i tabelę w panelu „Moje dzieci” i dostanie
            powiadomienia. Rodzic musi najpierw dołączyć do turnieju kodem.
          </DialogDescription>
        </DialogHeader>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="guardian-user">Osoba</FieldLabel>
            <Select value={userId} onValueChange={setUserId} disabled={members.isPending}>
              <SelectTrigger id="guardian-user" className="w-full">
                <SelectValue placeholder={members.isPending ? "Wczytywanie…" : "Wybierz osobę"} />
              </SelectTrigger>
              <SelectContent>
                {(members.data ?? []).map((m) => (
                  <SelectItem key={m.userId} value={m.userId}>
                    {m.firstName} {m.lastName} · {m.email}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {members.data?.length === 0 && (
              <p className="text-muted-foreground text-sm">
                Nikt jeszcze nie dołączył do turnieju - przekaż rodzicowi kod widza.
              </p>
            )}
          </Field>
          <Field>
            <FieldLabel htmlFor="guardian-relationship">Relacja</FieldLabel>
            <Select value={relationship} onValueChange={(v) => setRelationship(v as Relationship)}>
              <SelectTrigger id="guardian-relationship" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {RELATIONSHIPS.map((r) => (
                  <SelectItem key={r} value={r}>
                    {RELATIONSHIP_LABELS[r]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field orientation="horizontal">
            <Switch id="guardian-can-edit" checked={canEdit} onCheckedChange={setCanEdit} />
            <div>
              <FieldLabel htmlFor="guardian-can-edit">Może edytować profil</FieldLabel>
              <FieldDescription>
                Imię, nazwisko, data urodzenia, pozycja i zdjęcie dziecka.
              </FieldDescription>
            </div>
          </Field>
          {error && <FieldError>{error}</FieldError>}
        </FieldGroup>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={submit} disabled={!userId || assign.isPending}>
            {assign.isPending ? "Przypisywanie…" : "Przypisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
