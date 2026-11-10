"use client"

import { Wand2 } from "lucide-react"
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
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

type SecondStageSplitDialogProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  firstStage: FirstStageGroup[]
  secondStage: TournamentGroup[]
  /** Zapisany podział tej kategorii. */
  seats: SecondStageSeat[]
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
  trigger,
}: SecondStageSplitDialogProps) {
  const [open, setOpen] = useState(false)
  const secondStageIds = secondStage.map((g) => g.id)
  const proposal = () => assignmentFromSeats(proposeSplit(firstStage, secondStageIds))
  const [assignment, setAssignment] = useState<Record<string, string | null>>({})
  const save = useSaveSecondStageSeats(tournamentId)

  const sources = [...firstStage].sort((a, b) => a.order - b.order)
  const positions = Math.max(0, ...sources.map((g) => g.teams))
  const rows = Array.from({ length: positions }, (_, i) => i + 1).flatMap((position) =>
    sources
      .filter((g) => position <= g.teams)
      .map((g) => ({ key: placeKey(g.id, position), label: placeLabel(position, g.name) })),
  )

  function submit() {
    save.mutate(
      { categoryId, seats: seatsFromAssignment(assignment, firstStage, secondStageIds) },
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
        if (next) setAssignment(seats.length > 0 ? assignmentFromSeats(seats) : proposal())
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
        {rows.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            Grupy pierwszej fazy nie mają jeszcze drużyn, więc nie ma miejsc do podziału.
          </p>
        ) : (
          <div className="space-y-3">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setAssignment(proposal())}
            >
              <Wand2 />
              Gotowy podział
            </Button>
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
          <Button onClick={submit} disabled={save.isPending || rows.length === 0}>
            {save.isPending ? "Zapisywanie…" : "Zapisz podział"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
