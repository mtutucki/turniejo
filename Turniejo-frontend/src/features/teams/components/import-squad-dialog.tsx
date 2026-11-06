"use client"

import { History, Users } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { FieldError } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { isApiError } from "@/lib/api/errors"
import { formatDate, plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import {
  PLAYER_POSITION_LABELS,
  type RosterSource,
  type RosterSourcePlayer,
  type TournamentPlayer,
} from "@/types/teams"

import { useImportSquad, useRosterSources } from "../api"

type Row = { selected: boolean; shirtNumber: string }

function blockReason(player: RosterSourcePlayer) {
  if (player.alreadyInSquad) return "Już w składzie"
  if (player.ineligibility === "TooOld") return "Za stary na tę kategorię"
  if (player.ineligibility === "TooYoung") return "Za młody na tę kategorię"
  return null
}

/** Kadra nie ma zgłoszenia, więc dostaje stały klucz. */
const sourceKey = (source: RosterSource) => source.tournamentTeamId ?? "roster"

function sourceLabel(source: RosterSource) {
  if (source.kind === "Roster") return "Kadra drużyny"
  return [
    source.tournamentName,
    source.categoryName,
    source.tournamentStartAt && formatDate(source.tournamentStartAt),
  ]
    .filter(Boolean)
    .join(" · ")
}

/** Domyślnie wszyscy, których da się dodać; numer z poprzedniego turnieju, o ile jest wolny. */
function initialRows(source: RosterSource, squad: TournamentPlayer[]) {
  const taken = new Set(squad.map((p) => p.shirtNumber).filter((n) => n !== null))
  return Object.fromEntries(
    source.players.map((p): [string, Row] => [
      p.playerId,
      {
        selected: blockReason(p) === null,
        shirtNumber: p.shirtNumber !== null && !taken.has(p.shirtNumber) ? `${p.shirtNumber}` : "",
      },
    ]),
  )
}

function SourcePlayers({
  source,
  squad,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  source: RosterSource
  squad: TournamentPlayer[]
  isPending: boolean
  error: string | undefined
  onSubmit: (players: { player: RosterSourcePlayer; shirtNumber: number | null }[]) => void
  onCancel: () => void
}) {
  const [rows, setRows] = useState(() => initialRows(source, squad))
  const available = source.players.filter((p) => blockReason(p) === null)
  const selected = available.filter((p) => rows[p.playerId]!.selected)

  const takenInSquad = new Set(squad.map((p) => p.shirtNumber).filter((n) => n !== null))
  const numberCounts = new Map<string, number>()
  for (const p of selected) {
    const value = rows[p.playerId]!.shirtNumber.trim()
    if (value) numberCounts.set(value, (numberCounts.get(value) ?? 0) + 1)
  }

  function numberError(player: RosterSourcePlayer) {
    const row = rows[player.playerId]!
    const value = row.shirtNumber.trim()
    if (!row.selected || value === "") return null
    if (!/^\d+$/.test(value) || Number(value) < 1) return "Numer musi być liczbą większą od 0"
    if (takenInSquad.has(Number(value))) return "Numer zajęty w składzie"
    if ((numberCounts.get(value) ?? 0) > 1) return "Numer się powtarza"
    return null
  }

  const hasErrors = selected.some((p) => numberError(p) !== null)
  const allSelected = available.length > 0 && selected.length === available.length

  function update(playerId: string, patch: Partial<Row>) {
    setRows((current) => ({ ...current, [playerId]: { ...current[playerId]!, ...patch } }))
  }

  function toggleAll() {
    setRows((current) => {
      const next = { ...current }
      for (const p of available) next[p.playerId] = { ...next[p.playerId]!, selected: !allSelected }
      return next
    })
  }

  return (
    <>
      <div className="flex items-center justify-between gap-3 text-sm">
        <p className="text-muted-foreground">
          Zaznaczono {selected.length} z {available.length}
        </p>
        {available.length > 0 && (
          <Button type="button" variant="ghost" size="sm" onClick={toggleAll}>
            {allSelected ? "Odznacz wszystkich" : "Zaznacz wszystkich"}
          </Button>
        )}
      </div>

      <ul className="-mx-1 max-h-[50vh] divide-y overflow-y-auto px-1">
        {source.players.map((p) => {
          const reason = blockReason(p)
          const row = rows[p.playerId]!
          const rowError = numberError(p)
          const checkboxId = `import-${p.playerId}`
          return (
            <li key={p.playerId} className="py-2 text-sm">
              <div className="flex items-center gap-3">
                <Checkbox
                  id={checkboxId}
                  checked={row.selected}
                  disabled={reason !== null}
                  onCheckedChange={(checked) => update(p.playerId, { selected: checked === true })}
                />
                <label
                  htmlFor={checkboxId}
                  className={cn("min-w-0 flex-1", reason && "text-muted-foreground")}
                >
                  <span className="block truncate font-medium">
                    {p.firstName} {p.lastName}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    <span className="font-mono tabular-nums">{p.birthDate.slice(0, 4)}</span>
                    {p.position && ` · ${PLAYER_POSITION_LABELS[p.position]}`}
                    {reason && ` · ${reason}`}
                  </span>
                </label>
                {reason === null && (
                  <Input
                    aria-label={`Numer koszulki: ${p.firstName} ${p.lastName}`}
                    aria-invalid={rowError !== null}
                    inputMode="numeric"
                    placeholder="Nr"
                    className="w-16 text-center font-mono tabular-nums"
                    value={row.shirtNumber}
                    disabled={!row.selected}
                    onChange={(e) => update(p.playerId, { shirtNumber: e.target.value })}
                  />
                )}
              </div>
              {rowError && <p className="text-destructive mt-1 text-right text-xs">{rowError}</p>}
            </li>
          )
        })}
      </ul>

      {error && <FieldError>{error}</FieldError>}

      <DialogFooter>
        <Button type="button" variant="ghost" onClick={onCancel}>
          Anuluj
        </Button>
        <Button
          type="button"
          disabled={selected.length === 0 || hasErrors || isPending}
          onClick={() =>
            onSubmit(
              selected.map((player) => {
                const value = rows[player.playerId]!.shirtNumber.trim()
                return { player, shirtNumber: value ? Number(value) : null }
              }),
            )
          }
        >
          {isPending
            ? "Dodawanie…"
            : selected.length === 0
              ? "Dodaj do składu"
              : `Dodaj ${plural(selected.length, ["zawodnika", "zawodników", "zawodników"])}`}
        </Button>
      </DialogFooter>
    </>
  )
}

/**
 * Wybór zawodników do składu z kadry drużyny albo kopia składu z innego turnieju tej samej drużyny.
 * Przycisk pojawia się dopiero, gdy jest skąd brać zawodników.
 */
export function ImportSquadDialog({
  tournamentId,
  tournamentTeamId,
  squad,
}: {
  tournamentId: string
  tournamentTeamId: string
  squad: TournamentPlayer[]
}) {
  const [open, setOpen] = useState(false)
  const [sourceId, setSourceId] = useState<string>()
  const [error, setError] = useState<string>()
  const sources = useRosterSources(tournamentId, tournamentTeamId)
  const importSquad = useImportSquad(tournamentId, tournamentTeamId)

  if (!sources.data?.length) return null

  // Kadra pierwsza, potem turnieje od najnowszego. Pierwsze źródło jest domyślne.
  const source = sources.data.find((s) => sourceKey(s) === sourceId) ?? sources.data[0]!
  const fromRoster = source.kind === "Roster"
  const hasRoster = sources.data[0]!.kind === "Roster"

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setSourceId(undefined)
          setError(undefined)
        }
      }}
    >
      <DialogTrigger asChild>
        <Button size="sm" variant="outline">
          {hasRoster ? <Users /> : <History />}
          {hasRoster ? "Dodaj z kadry" : "Wczytaj skład"}
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {fromRoster ? "Zawodnicy z kadry" : "Skład z poprzedniego turnieju"}
          </DialogTitle>
          <DialogDescription>
            Zaznacz, kto gra w tym turnieju. Numery możesz zmienić od razu, resztę później w edycji
            zawodnika.
          </DialogDescription>
        </DialogHeader>

        {sources.data.length > 1 ? (
          <Select
            value={sourceKey(source)}
            onValueChange={(value) => {
              setSourceId(value)
              setError(undefined)
            }}
          >
            <SelectTrigger aria-label="Skąd wziąć zawodników" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {sources.data.map((s) => (
                <SelectItem key={sourceKey(s)} value={sourceKey(s)}>
                  {sourceLabel(s)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : (
          <p className="text-sm font-medium">{sourceLabel(source)}</p>
        )}

        {/* Montowane przy otwarciu i per źródło — zaznaczenia zawsze startują od domyślnych. */}
        {open && (
          <SourcePlayers
            key={sourceKey(source)}
            source={source}
            squad={squad}
            isPending={importSquad.isPending}
            error={error}
            onCancel={() => setOpen(false)}
            onSubmit={(players) => {
              setError(undefined)
              importSquad.mutate(
                {
                  sourceTournamentTeamId: source.tournamentTeamId,
                  players: players.map(({ player, shirtNumber }) => ({
                    playerId: player.playerId,
                    shirtNumber,
                    position: player.position,
                  })),
                },
                {
                  onSuccess: () => {
                    toast.success(
                      `Dodano ${plural(players.length, ["zawodnika", "zawodników", "zawodników"])} do składu`,
                    )
                    setOpen(false)
                  },
                  onError: (e) =>
                    setError(
                      isApiError(e)
                        ? (e.fieldErrors[0]?.message ?? e.detail ?? e.message)
                        : undefined,
                    ),
                },
              )
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
