"use client"

import { BarChart3, Lock, Pencil, Users } from "lucide-react"
import { useState } from "react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { zodResolver } from "@hookform/resolvers/zod"

import { TextField } from "@/components/forms/text-field"
import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import {
  useCoachPlayers,
  useCoachResults,
  useCoachSchedule,
  useCoachStats,
  useUpdateSquadPlayer,
} from "@/features/panels/api"
import { SimpleMatchList } from "@/features/panels/components/simple-match-list"
import { AddPlayerDialog } from "@/features/teams/components/add-player-dialog"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import {
  PLAYER_POSITION_LABELS,
  PLAYER_STATUS_LABELS,
  type PlayerPosition,
  type TournamentPlayer,
  type TournamentPlayerStatus,
} from "@/types/teams"

import { useCoachTeam } from "./team-shell"

const POSITIONS = Object.keys(PLAYER_POSITION_LABELS) as PlayerPosition[]
const STATUSES = Object.keys(PLAYER_STATUS_LABELS) as TournamentPlayerStatus[]
const NO_POSITION = "none"

// Numer 1–99 jak w UpdateTournamentPlayerCommandValidator; puste pole = bez numeru.
const squadSchema = z.object({
  shirtNumber: z
    .string()
    .trim()
    .refine(
      (v) => v === "" || (/^\d+$/.test(v) && Number(v) >= 1 && Number(v) <= 99),
      "Numer od 1 do 99",
    ),
  position: z.string(),
  status: z.string(),
})
type SquadValues = z.infer<typeof squadSchema>

function EditSquadPlayerDialog({
  player,
  squad,
  onClose,
}: {
  player: TournamentPlayer
  squad: TournamentPlayer[]
  onClose: () => void
}) {
  const team = useCoachTeam()
  const update = useUpdateSquadPlayer(team.tournamentId, team.tournamentTeamId)
  const form = useForm<SquadValues>({
    resolver: zodResolver(
      squadSchema.refine(
        (v) =>
          v.shirtNumber === "" ||
          !squad.some((p) => p.id !== player.id && p.shirtNumber === Number(v.shirtNumber)),
        { path: ["shirtNumber"], message: "Ten numer ma już inny zawodnik" },
      ),
    ),
    defaultValues: {
      shirtNumber: player.shirtNumber?.toString() ?? "",
      position: player.position ?? NO_POSITION,
      status: player.status,
    },
  })
  const { errors } = form.formState

  function submit(values: SquadValues) {
    update.mutate(
      {
        tournamentPlayerId: player.id,
        shirtNumber: values.shirtNumber ? Number(values.shirtNumber) : null,
        position: values.position === NO_POSITION ? null : (values.position as PlayerPosition),
        status: values.status as TournamentPlayerStatus,
      },
      {
        onSuccess: () => {
          toast.success("Zapisano zmiany w składzie")
          onClose()
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {player.firstName} {player.lastName}
          </DialogTitle>
        </DialogHeader>
        <form id="squad-form" onSubmit={form.handleSubmit(submit)} noValidate>
          <FieldGroup>
            <TextField
              id="squad-number"
              label="Numer na koszulce"
              inputMode="numeric"
              error={errors.shirtNumber}
              {...form.register("shirtNumber")}
            />
            <Controller
              control={form.control}
              name="position"
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="squad-position">Pozycja</FieldLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="squad-position" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NO_POSITION}>Bez pozycji</SelectItem>
                      {POSITIONS.map((p) => (
                        <SelectItem key={p} value={p}>
                          {PLAYER_POSITION_LABELS[p]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              )}
            />
            <Controller
              control={form.control}
              name="status"
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="squad-status">Status</FieldLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="squad-status" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {STATUSES.map((s) => (
                        <SelectItem key={s} value={s}>
                          {PLAYER_STATUS_LABELS[s]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
              )}
            />
            <FieldError errors={[errors.root]} />
          </FieldGroup>
        </form>
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>
            Anuluj
          </Button>
          <Button type="submit" form="squad-form" disabled={update.isPending}>
            {update.isPending ? "Zapisywanie…" : "Zapisz"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function CoachPlayersView() {
  const team = useCoachTeam()
  const players = useCoachPlayers(team.tournamentTeamId)
  const [editing, setEditing] = useState<TournamentPlayer | null>(null)
  // Skład zmienia tylko trener (AssistantCoach/Manager widzą go read-only — CoachOnly na backendzie).
  const isCoach = team.role === "Coach"
  const canEdit = isCoach && team.canEditSquad
  const squad = [...(players.data ?? [])].sort(
    (a, b) =>
      (a.shirtNumber ?? 999) - (b.shirtNumber ?? 999) || a.lastName.localeCompare(b.lastName, "pl"),
  )

  return (
    <div className="space-y-4">
      {isCoach && !team.canEditSquad && (
        <div
          role="status"
          data-testid="squad-locked"
          className="bg-muted text-muted-foreground flex items-start gap-3 rounded-xl border px-4 py-3 text-sm"
        >
          <Lock className="mt-0.5 size-4 shrink-0" />
          <p>
            Turniej już trwa — organizator zablokował zmiany w składzie. O zmianę (np. kontuzję)
            poproś organizatora.
          </p>
        </div>
      )}

      <div className="flex items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">
          {plural(squad.length, ["zawodnik", "zawodników", "zawodników"])}
        </p>
        {canEdit && (
          <AddPlayerDialog
            tournamentId={team.tournamentId}
            tournamentTeamId={team.tournamentTeamId}
            category={undefined}
            squad={squad}
          />
        )}
      </div>

      {players.isPending ? (
        <Skeleton className="h-40 w-full rounded-xl" />
      ) : !squad.length ? (
        <EmptyState icon={Users} title="Skład jest pusty" />
      ) : (
        <ul className="bg-card divide-y rounded-xl border">
          {squad.map((p) => (
            <li key={p.id} className="flex items-center gap-3 px-4 py-2 text-sm">
              <span className="text-primary w-8 text-right font-mono tabular-nums">
                {p.shirtNumber ?? "–"}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium">
                  {p.firstName} {p.lastName}
                </span>
                <span className="text-muted-foreground text-xs">
                  {p.birthDate && (
                    <span className="font-mono tabular-nums">{p.birthDate.slice(0, 4)}</span>
                  )}
                  {p.position && ` · ${PLAYER_POSITION_LABELS[p.position]}`}
                </span>
              </span>
              {p.status !== "Active" && (
                <span
                  className={cn(
                    "text-xs",
                    p.status === "Injured" || p.status === "Suspended"
                      ? "text-destructive"
                      : "text-muted-foreground",
                  )}
                >
                  {PLAYER_STATUS_LABELS[p.status]}
                </span>
              )}
              {canEdit && (
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Edytuj ${p.firstName} ${p.lastName}`}
                  onClick={() => setEditing(p)}
                >
                  <Pencil />
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <EditSquadPlayerDialog player={editing} squad={squad} onClose={() => setEditing(null)} />
      )}
    </div>
  )
}

export function CoachScheduleView() {
  const team = useCoachTeam()
  const schedule = useCoachSchedule(team.tournamentTeamId)
  const results = useCoachResults(team.tournamentTeamId)
  return (
    <div className="space-y-6">
      <section className="space-y-2">
        <h2 className="font-semibold">Nadchodzące mecze</h2>
        <SimpleMatchList
          matches={schedule.data}
          isPending={schedule.isPending}
          highlightTeam={team.teamName}
          emptyTitle="Brak zaplanowanych meczów"
        />
      </section>
      <section className="space-y-2">
        <h2 className="font-semibold">Wyniki</h2>
        <SimpleMatchList
          matches={
            results.data
              ? [...results.data].sort((a, b) => b.scheduledAt.localeCompare(a.scheduledAt))
              : undefined
          }
          isPending={results.isPending}
          highlightTeam={team.teamName}
          emptyTitle="Brak rozegranych meczów"
        />
      </section>
    </div>
  )
}

export function CoachStatsView() {
  const team = useCoachTeam()
  const stats = useCoachStats(team.tournamentTeamId)

  if (stats.isPending) return <Skeleton className="h-40 w-full rounded-xl" />
  if (!stats.data?.length) return <EmptyState icon={BarChart3} title="Brak statystyk zawodników" />

  const rows = [...stats.data].sort(
    (a, b) =>
      b.goals - a.goals || b.assists - a.assists || a.lastName.localeCompare(b.lastName, "pl"),
  )

  return (
    <div className="bg-card overflow-x-auto rounded-xl border">
      <table className="w-full text-sm">
        <thead className="text-muted-foreground text-xs">
          <tr>
            <th className="px-4 py-2 text-left font-medium">Zawodnik</th>
            <th className="w-14 px-2 py-2 text-center font-medium" title="Bramki">
              G
            </th>
            <th className="w-14 px-2 py-2 text-center font-medium" title="Asysty">
              A
            </th>
            <th className="w-14 px-2 py-2 text-center font-medium" title="Żółte kartki">
              <span
                className="inline-block h-3.5 w-2.5 rounded-xs bg-yellow-400 align-middle"
                role="img"
                aria-label="Żółte kartki"
              />
            </th>
            <th className="w-14 px-2 py-2 text-center font-medium" title="Czerwone kartki">
              <span
                className="inline-block h-3.5 w-2.5 rounded-xs bg-red-500 align-middle"
                role="img"
                aria-label="Czerwone kartki"
              />
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.tournamentPlayerId} className="border-t">
              <td className="truncate px-4 py-2 font-medium">
                {r.firstName} {r.lastName}
              </td>
              <td className="px-2 py-2 text-center font-mono tabular-nums">{r.goals}</td>
              <td className="px-2 py-2 text-center font-mono tabular-nums">{r.assists}</td>
              <td className="px-2 py-2 text-center font-mono tabular-nums">{r.yellowCards}</td>
              <td className="px-2 py-2 text-center font-mono tabular-nums">{r.redCards}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
