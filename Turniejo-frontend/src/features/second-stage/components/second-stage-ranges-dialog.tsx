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
import { Input } from "@/components/ui/input"
import type { SecondStageRange, SecondStageRangeInput, TournamentGroup } from "@/types/tournaments"

import { useSaveSecondStageRanges } from "../api"
import { type FirstStageGroup, largestGroupEstimate, proposeRanges, rangesOverlap } from "../split"

const MAX_POSITION = 64

type SecondStageRangesDialogProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  firstStage: FirstStageGroup[]
  secondStage: TournamentGroup[]
  /** Zapisane zakresy tej kategorii. */
  ranges: SecondStageRange[]
  /** Zgłoszone drużyny kategorii, także bez grupy - do gotowego podziału, zanim drużyny są w grupach. */
  categoryTeams: number
  trigger: ReactNode
}

/**
 * Podział na drugą fazę zakresami: przy każdej grupie fazy 2 miejsca od–do, z każdej grupy fazy 1.
 * Działa, zanim grupy mają drużyny; konkretne miejsca powstaną przy generowaniu terminarza.
 */
export function SecondStageRangesDialog({
  tournamentId,
  categoryId,
  categoryName,
  firstStage,
  secondStage,
  ranges,
  categoryTeams,
  trigger,
}: SecondStageRangesDialogProps) {
  const [open, setOpen] = useState(false)
  const [values, setValues] = useState<Record<string, { from: string; to: string }>>({})
  const save = useSaveSecondStageRanges(tournamentId)
  const groups = [...secondStage].sort((a, b) => a.order - b.order)

  const largest = largestGroupEstimate(firstStage, categoryTeams)

  const fromInputs = (list: SecondStageRangeInput[]) =>
    Object.fromEntries(
      groups.map((g) => {
        const r = list.find((x) => x.groupId === g.id)
        return [g.id, { from: r ? String(r.fromPosition) : "", to: r ? String(r.toPosition) : "" }]
      }),
    )

  const parsed = groups.flatMap((g) => {
    const v = values[g.id]
    if (!v || (v.from === "" && v.to === "")) return []
    return [{ groupId: g.id, fromPosition: Number(v.from), toPosition: Number(v.to || v.from) }]
  })
  const error = parsed.some(
    (r) =>
      !Number.isInteger(r.fromPosition) ||
      !Number.isInteger(r.toPosition) ||
      r.fromPosition < 1 ||
      r.toPosition > MAX_POSITION,
  )
    ? "Miejsca to liczby od 1."
    : parsed.some((r) => r.toPosition < r.fromPosition)
      ? "Zakres idzie od niższego miejsca do wyższego, np. 1-2."
      : rangesOverlap(parsed)
        ? "To samo miejsce nie może trafić do dwóch grup."
        : null

  function submit() {
    save.mutate(
      { categoryId, ranges: parsed },
      {
        onSuccess: () => {
          toast.success("Podział zapisany")
          setOpen(false)
        },
      },
    )
  }

  const update = (groupId: string, key: "from" | "to", value: string) =>
    setValues((prev) => ({
      ...prev,
      [groupId]: { ...(prev[groupId] ?? { from: "", to: "" }), [key]: value },
    }))

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next)
          setValues(
            fromInputs(
              ranges.length > 0
                ? ranges
                : proposeRanges(
                    largest,
                    groups.map((g) => g.id),
                  ),
            ),
          )
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Podział na drugą fazę · {categoryName}</DialogTitle>
          <DialogDescription>
            Które miejsca z każdej grupy pierwszej fazy grają dalej w danej grupie. Grupa z mniejszą
            liczbą drużyn po prostu nie ma dalszych miejsc, a miejsca spoza podziału kończą turniej.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <ul className="divide-y rounded-md border" aria-label="Zakresy miejsc">
            {groups.map((g) => (
              <li key={g.id} className="flex flex-wrap items-center gap-2 py-2 pr-2 pl-3">
                <span className="min-w-0 flex-1 truncate text-sm font-medium">{g.name}</span>
                <span className="text-muted-foreground text-sm">miejsca</span>
                <Input
                  type="number"
                  inputMode="numeric"
                  min={1}
                  aria-label={`${g.name}: od miejsca`}
                  value={values[g.id]?.from ?? ""}
                  onChange={(e) => update(g.id, "from", e.target.value)}
                  className="w-16 font-mono tabular-nums"
                />
                <span className="text-muted-foreground text-sm">–</span>
                <Input
                  type="number"
                  inputMode="numeric"
                  min={1}
                  aria-label={`${g.name}: do miejsca`}
                  value={values[g.id]?.to ?? ""}
                  onChange={(e) => update(g.id, "to", e.target.value)}
                  className="w-16 font-mono tabular-nums"
                />
              </li>
            ))}
          </ul>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() =>
              setValues(
                fromInputs(
                  proposeRanges(
                    largest,
                    groups.map((g) => g.id),
                  ),
                ),
              )
            }
          >
            <Wand2 />
            Gotowy podział
          </Button>
          {error && (
            <p className="text-destructive text-sm" role="alert">
              {error}
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Anuluj
          </Button>
          <Button onClick={submit} disabled={save.isPending || !!error}>
            {save.isPending ? "Zapisywanie…" : "Zapisz podział"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
