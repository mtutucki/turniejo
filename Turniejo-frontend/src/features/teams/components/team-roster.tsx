"use client"

import { ChevronDown, Pencil, RotateCcw, UserMinus, UserPlus, Users } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/layout/empty-state"
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Skeleton } from "@/components/ui/skeleton"
import { isApiError } from "@/lib/api/errors"
import { formatDate, plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import {
  PLAYER_POSITION_LABELS,
  type TeamRosterPlayer,
  type TeamRosterSquad,
  type TournamentPlayer,
} from "@/types/teams"

import {
  useRemoveRosterPlayer,
  useRestoreRosterPlayer,
  useSaveRosterPlayer,
  useTeamRoster,
} from "../api"
import { PlayerForm } from "./player-form"

const fullName = (p: TeamRosterPlayer) => `${p.firstName} ${p.lastName}`

/** PlayerForm pracuje na kształcie wpisu ze składu, więc zawodnika z kadry podajemy w tej postaci. */
function asFormPlayer(p: TeamRosterPlayer): TournamentPlayer {
  return {
    id: p.playerId,
    tournamentTeamId: "",
    playerId: p.playerId,
    shirtNumber: p.shirtNumber,
    position: p.defaultPosition,
    status: "Active",
    firstName: p.firstName,
    lastName: p.lastName,
    birthDate: p.birthDate,
    photoUrl: p.photoUrl,
  }
}

function squadLabel(squad: TeamRosterSquad) {
  return squad.tournamentStatus === "InProgress"
    ? `${squad.tournamentName} (trwa)`
    : squad.tournamentName
}

/**
 * Kadra drużyny z bazy: zawodnicy należą do drużyny niezależnie od turniejów, a skład na turniej
 * wybiera się z kadry. Używana w bazie drużyn (organizator) i w panelu trenera.
 */
export function TeamRoster({ teamId }: { teamId: string }) {
  const roster = useTeamRoster(teamId)
  const [editing, setEditing] = useState<TeamRosterPlayer | "new" | null>(null)
  const [removing, setRemoving] = useState<TeamRosterPlayer | null>(null)
  const [showFormer, setShowFormer] = useState(false)
  const restore = useRestoreRosterPlayer(teamId)

  if (roster.isPending) return <Skeleton className="h-48 w-full rounded-xl" />
  if (roster.isError) {
    return (
      <EmptyState
        icon={Users}
        title={
          isApiError(roster.error) && roster.error.status === 403
            ? "Nie masz dostępu do kadry tej drużyny"
            : "Nie udało się pobrać kadry"
        }
      />
    )
  }

  const { canEdit, players } = roster.data
  const active = players.filter((p) => p.status === "Active")
  const former = players.filter((p) => p.status === "Left")

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">
          {plural(active.length, ["zawodnik", "zawodników", "zawodników"])} w kadrze
        </p>
        {canEdit && (
          <Button size="sm" onClick={() => setEditing("new")}>
            <UserPlus />
            Dodaj zawodnika
          </Button>
        )}
      </div>

      {active.length === 0 ? (
        <EmptyState
          icon={Users}
          title="Kadra jest pusta"
          description="Dodaj zawodników drużyny. Skład na turniej wybierzesz potem z kadry."
        />
      ) : (
        <ul className="bg-card divide-y rounded-xl border">
          {active.map((p) => (
            <li key={p.playerId} className="flex items-center gap-3 px-4 py-2 text-sm">
              <span className="text-primary w-8 text-right font-mono tabular-nums">
                {p.shirtNumber ?? "-"}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium">{fullName(p)}</span>
                <span className="text-muted-foreground block truncate text-xs">
                  <span className="font-mono tabular-nums">{p.birthDate.slice(0, 4)}</span>
                  {p.defaultPosition !== "Unknown" &&
                    ` · ${PLAYER_POSITION_LABELS[p.defaultPosition]}`}
                  {p.currentSquads.length > 0 &&
                    ` · W składzie: ${p.currentSquads.map(squadLabel).join(", ")}`}
                </span>
              </span>
              {canEdit && (
                <div className="flex shrink-0">
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Edytuj ${fullName(p)}`}
                    onClick={() => setEditing(p)}
                  >
                    <Pencil />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Usuń ${fullName(p)} z kadry`}
                    onClick={() => setRemoving(p)}
                  >
                    <UserMinus />
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {former.length > 0 && (
        <section className="space-y-2">
          <Button
            variant="ghost"
            size="sm"
            className="-ml-2"
            aria-expanded={showFormer}
            onClick={() => setShowFormer((v) => !v)}
          >
            <ChevronDown className={cn("transition-transform", showFormer && "rotate-180")} />
            Byli zawodnicy ({former.length})
          </Button>
          {showFormer && (
            <>
              <p className="text-muted-foreground text-xs">
                Nie ma ich już w kadrze, ale zostają w historii turniejów, w których grali.
              </p>
              <ul className="divide-y rounded-xl border border-dashed">
                {former.map((p) => (
                  <li key={p.playerId} className="flex items-center gap-3 px-4 py-2 text-sm">
                    <span className="min-w-0 flex-1">
                      <span className="text-muted-foreground block truncate font-medium">
                        {fullName(p)}
                      </span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {p.leftAt && `Poza kadrą od ${formatDate(p.leftAt)}`}
                        {p.pastTournamentsCount > 0 &&
                          ` · ${plural(p.pastTournamentsCount, ["turniej", "turnieje", "turniejów"])} w historii`}
                        {p.currentSquads.length > 0 &&
                          ` · Wciąż w składzie: ${p.currentSquads.map(squadLabel).join(", ")}`}
                      </span>
                    </span>
                    {canEdit && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={restore.isPending}
                        onClick={() =>
                          restore.mutate(p.playerId, {
                            onSuccess: (result) =>
                              toast.success(
                                p.shirtNumber !== null && result.shirtNumber === null
                                  ? `${fullName(p)} znów w kadrze, bez numeru: ${p.shirtNumber} ma już ktoś inny.`
                                  : `${fullName(p)} znów w kadrze`,
                              ),
                          })
                        }
                      >
                        <RotateCcw />
                        Przywróć
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      )}

      {editing && (
        <RosterPlayerDialog
          teamId={teamId}
          members={active}
          player={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      )}
      {removing && (
        <RemoveRosterPlayerDialog
          teamId={teamId}
          player={removing}
          onClose={() => setRemoving(null)}
        />
      )}
    </div>
  )
}

function RosterPlayerDialog({
  teamId,
  members,
  player,
  onClose,
}: {
  teamId: string
  members: TeamRosterPlayer[]
  player: TeamRosterPlayer | undefined
  onClose: () => void
}) {
  const save = useSaveRosterPlayer(teamId)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{player ? "Edycja zawodnika" : "Nowy zawodnik w kadrze"}</DialogTitle>
          <DialogDescription>
            {player
              ? "Imię, nazwisko i data urodzenia zmienią się też w składach turniejów. Numer to podpowiedź przy dodawaniu do składu."
              : "Numer to podpowiedź przy dodawaniu do składu na turniej. W składzie możesz go zmienić."}
          </DialogDescription>
        </DialogHeader>
        <PlayerForm
          mode="roster"
          category={undefined}
          squad={members.map(asFormPlayer)}
          player={player && asFormPlayer(player)}
          submitLabel={player ? "Zapisz" : "Dodaj do kadry"}
          onSubmit={async (input) => {
            await save.mutateAsync({
              playerId: player?.playerId,
              firstName: input.firstName,
              lastName: input.lastName,
              birthDate: input.birthDate,
              position: input.position,
              shirtNumber: input.shirtNumber,
              allowDuplicate: input.allowDuplicate,
            })
            toast.success(
              player ? "Zapisano zmiany" : `${input.firstName} ${input.lastName} w kadrze`,
            )
            onClose()
          }}
          secondaryAction={
            <Button type="button" variant="ghost" onClick={onClose}>
              Anuluj
            </Button>
          }
        />
      </DialogContent>
    </Dialog>
  )
}

/**
 * Usunięcie z kadry. Gdy zawodnik jest w składzie turnieju, który trwa albo się nie zaczął,
 * usuwający decyduje, czy zdjąć go też z tych składów. Zakończone turnieje zostają nietknięte.
 */
function RemoveRosterPlayerDialog({
  teamId,
  player,
  onClose,
}: {
  teamId: string
  player: TeamRosterPlayer
  onClose: () => void
}) {
  const remove = useRemoveRosterPlayer(teamId)
  const squads = player.currentSquads
  const editable = squads.filter((s) => s.canEditSquad)
  const locked = squads.filter((s) => !s.canEditSquad)
  const [fromSquads, setFromSquads] = useState(editable.length > 0)
  const name = fullName(player)

  function confirm() {
    remove.mutate(
      { playerId: player.playerId, removeFromCurrentSquads: fromSquads && editable.length > 0 },
      {
        onSuccess: (result) => {
          toast.success(`Usunięto ${name} z kadry`)
          if (result.keptInSquads.length > 0) {
            toast.warning(
              `${name} jest nadal w składzie: ${result.keptInSquads.join(", ")}. Ten skład jest zablokowany.`,
            )
          }
          onClose()
        },
      },
    )
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Usunąć {name} z kadry?</AlertDialogTitle>
          <AlertDialogDescription>
            {player.pastTournamentsCount > 0 || squads.length > 0
              ? "Profil zostaje. Zawodnik trafi do byłych zawodników i nadal będzie widoczny w turniejach, w których grał."
              : "Zawodnik nie grał jeszcze w żadnym turnieju tej drużyny."}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {editable.length > 0 && (
          <fieldset className="space-y-2 text-sm">
            <legend className="mb-2 font-medium">
              Jest w składzie: {editable.map(squadLabel).join(", ")}. Co z tym składem?
            </legend>
            <RadioOption
              name="from-squads"
              checked={fromSquads}
              onChange={() => setFromSquads(true)}
              label="Usuń też ze składu"
              hint="Gole i kartki z rozegranych meczów zostaną w historii."
            />
            <RadioOption
              name="from-squads"
              checked={!fromSquads}
              onChange={() => setFromSquads(false)}
              label="Zostaw w składzie"
              hint="Zagra jeszcze w tym turnieju, ale nie będzie proponowany do kolejnych."
            />
          </fieldset>
        )}

        {locked.length > 0 && (
          <p className="bg-muted text-muted-foreground rounded-md px-3 py-2 text-sm">
            Skład {locked.map(squadLabel).join(", ")} jest zablokowany, więc zawodnik w nim
            zostanie. Zmienić go może organizator turnieju.
          </p>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>Anuluj</AlertDialogCancel>
          <Button variant="destructive" disabled={remove.isPending} onClick={confirm}>
            {remove.isPending ? "Usuwanie…" : "Usuń z kadry"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function RadioOption({
  name,
  checked,
  onChange,
  label,
  hint,
}: {
  name: string
  checked: boolean
  onChange: () => void
  label: string
  hint: string
}) {
  return (
    <label
      className={cn(
        "flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2",
        checked && "border-primary bg-primary/5",
      )}
    >
      <input
        type="radio"
        name={name}
        checked={checked}
        onChange={onChange}
        className="accent-primary mt-1"
      />
      <span>
        <span className="block font-medium">{label}</span>
        <span className="text-muted-foreground block text-xs">{hint}</span>
      </span>
    </label>
  )
}
