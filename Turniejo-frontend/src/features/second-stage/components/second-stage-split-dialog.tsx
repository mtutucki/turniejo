"use client"

import { Wand2 } from "lucide-react"
import { type ReactNode, useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { SecondStageSeat, TournamentGroup } from "@/types/tournaments"

import { useSaveSecondStageSeats } from "../api"
import {
  assignmentFromSeats,
  type FirstStageGroup,
  placeKey,
  placeLabel,
  proposeSplit,
  seatsFromAssignment,
} from "../split"

const OUT = "out"
const DEFAULT_GROUP_SIZE = 4
const MAX_GROUP_SIZE = 16

type SecondStageSplitDialogProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  firstStage: FirstStageGroup[]
  secondStage: TournamentGroup[]
  /** Zapisany podział tej kategorii. */
  seats: SecondStageSeat[]
  /** Zgłoszone drużyny kategorii (także bez grupy) - do domyślnej liczby miejsc i kontroli sumy. */
  categoryTeams: number
  /** categoryTeams pochodzi z limitu drużyn turnieju, a nie ze zgłoszeń. */
  teamsFromLimit?: boolean
  trigger: ReactNode
}

/**
 * Które miejsce z tabel fazy 1 trafia do której grupy fazy 2. Bez zapisanego podziału okno startuje
 * od gotowej propozycji (proposeSplit); każde miejsce można przestawić albo zostawić poza fazą 2.
 */
export function SecondStageSplitDialog({
  tournamentId,
  categoryId,
  categoryName,
  firstStage,
  secondStage,
  seats,
  categoryTeams,
  teamsFromLimit = false,
  trigger,
}: SecondStageSplitDialogProps) {
  const [open, setOpen] = useState(false)
  const secondStageIds = secondStage.map((g) => g.id)
  const sources = [...firstStage].sort((a, b) => a.order - b.order)
  // Grupa z drużynami ma tyle miejsc, ile drużyn. Podział ustawia się często przed przydziałem drużyn
  // do grup (kreator), więc dla grup bez drużyn liczbę miejsc się wpisuje. Domyślnie: drużyny kategorii
  // bez grupy po równo na te grupy (8 drużyn i 4 puste grupy = po 2), potem największa grupa,
  // zapisany podział, a bez żadnych drużyn 4.
  const withoutTeams = sources.filter((g) => g.teams === 0)
  const assigned = sources.reduce((sum, g) => sum + g.teams, 0)
  const unassigned = Math.max(0, categoryTeams - assigned)
  const defaultSize =
    (withoutTeams.length > 0 && unassigned > 0 ? Math.ceil(unassigned / withoutTeams.length) : 0) ||
    Math.max(...sources.map((g) => g.teams), 0) ||
    Math.max(...seats.map((x) => x.sourcePosition), 0) ||
    DEFAULT_GROUP_SIZE
  const [size, setSize] = useState(defaultSize)
  const sized = (n: number) => sources.map((g) => ({ ...g, teams: g.teams > 0 ? g.teams : n }))
  const proposal = (n: number) => assignmentFromSeats(proposeSplit(sized(n), secondStageIds))
  const [assignment, setAssignment] = useState<Record<string, string | null>>({})
  const save = useSaveSecondStageSeats(tournamentId)

  const limits = sized(size)
  const rows = Array.from(
    { length: Math.max(0, ...limits.map((g) => g.teams)) },
    (_, i) => i + 1,
  ).flatMap((position) =>
    limits
      .filter((g) => position <= g.teams)
      .map((g) => ({ key: placeKey(g.id, position), label: placeLabel(position, g.name) })),
  )
  const smaller = limits.filter((g) => g.teams < Math.max(...limits.map((x) => x.teams)))
  const totalPlaces = limits.reduce((sum, g) => sum + g.teams, 0)

  function changeSize(value: number) {
    const n = Math.min(MAX_GROUP_SIZE, Math.max(1, Math.floor(value) || 1))
    setSize(n)
    setAssignment(proposal(n))
  }

  function submit() {
    save.mutate(
      {
        categoryId,
        // Miejsca, których grupa nie ma (np. zapisane, zanim zgłosiły się drużyny), wypadają z podziału.
        seats: seatsFromAssignment(
          Object.fromEntries(rows.map((row) => [row.key, assignment[row.key] ?? null])),
          limits,
          secondStageIds,
        ),
      },
      {
        onSuccess: () => {
          toast.success("Podział zapisany")
          setOpen(false)
        },
      },
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (!next) return
        setSize(defaultSize)
        setAssignment(seats.length > 0 ? assignmentFromSeats(seats) : proposal(defaultSize))
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[90dvh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Podział na drugą fazę · {categoryName}</DialogTitle>
          <DialogDescription>
            Wybierz, do której grupy drugiej fazy trafia drużyna z każdego miejsca. Drużyna spoza
            podziału kończy turniej po pierwszej fazie.
          </DialogDescription>
        </DialogHeader>
        {sources.length === 0 ? (
          <p className="text-muted-foreground text-sm">Najpierw dodaj grupy pierwszej fazy.</p>
        ) : (
          <div className="space-y-3">
            <div className="flex flex-wrap items-end gap-3">
              {withoutTeams.length > 0 && (
                <div className="space-y-1">
                  <Label htmlFor="second-stage-size">
                    {withoutTeams.length === sources.length
                      ? "Drużyn w grupie"
                      : "Drużyn w grupach bez drużyn"}
                  </Label>
                  <Input
                    id="second-stage-size"
                    type="number"
                    inputMode="numeric"
                    min={1}
                    max={MAX_GROUP_SIZE}
                    value={size}
                    onChange={(e) => changeSize(Number(e.target.value))}
                    className="w-24 font-mono tabular-nums"
                  />
                </div>
              )}
              <Button type="button" variant="outline" onClick={() => setAssignment(proposal(size))}>
                <Wand2 />
                Gotowy podział
              </Button>
            </div>
            <p
              className={cn(
                "text-xs",
                categoryTeams > 0 && totalPlaces > categoryTeams
                  ? "text-destructive"
                  : "text-muted-foreground",
              )}
            >
              Razem miejsc: {totalPlaces} · {teamsFromLimit ? "limit drużyn" : "drużyn w kategorii"}
              : {categoryTeams}
              {categoryTeams > 0 &&
                totalPlaces > categoryTeams &&
                ". Miejsc jest więcej niż drużyn."}
            </p>
            {smaller.length > 0 && (
              <p className="text-muted-foreground text-xs">
                {smaller
                  .map((g) => `${g.name} ma ${plural(g.teams, ["drużynę", "drużyny", "drużyn"])}`)
                  .join(", ")}
                , więc ma mniej miejsc.
              </p>
            )}
            <ul className="divide-y rounded-md border" aria-label="Miejsca z pierwszej fazy">
              {rows.map((row) => (
                <li key={row.key} className="flex items-center gap-2 py-1.5 pr-1.5 pl-3">
                  <span className="min-w-0 flex-1 truncate text-sm">{row.label}</span>
                  <Select
                    value={assignment[row.key] ?? OUT}
                    onValueChange={(value) =>
                      setAssignment((prev) => ({
                        ...prev,
                        [row.key]: value === OUT ? null : value,
                      }))
                    }
                  >
                    <SelectTrigger
                      className="h-9 w-36 shrink-0"
                      aria-label={`Grupa dla: ${row.label}`}
                    >
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {secondStage.map((g) => (
                        <SelectItem key={g.id} value={g.id}>
                          {g.name}
                        </SelectItem>
                      ))}
                      <SelectItem value={OUT}>Nie awansuje</SelectItem>
                    </SelectContent>
                  </Select>
                </li>
              ))}
            </ul>
          </div>
        )}
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={submit} disabled={save.isPending || sources.length === 0}>
            {save.isPending ? "Zapisywanie…" : "Zapisz podział"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
