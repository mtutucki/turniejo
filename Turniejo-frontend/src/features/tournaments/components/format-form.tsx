"use client"

import { GitBranch, type LucideIcon, ListOrdered, Network } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentFormat,
} from "@/types/tournaments"

import { useChangeTournamentFormat } from "../api"

const FORMATS: { value: TournamentFormat; description: string; icon: LucideIcon }[] = [
  {
    value: "Group",
    description: "Każdy z każdym w grupach. O miejscu decyduje tabela.",
    icon: ListOrdered,
  },
  {
    value: "Knockout",
    description: "Drabinka od pierwszego meczu — przegrany odpada.",
    icon: GitBranch,
  },
  {
    value: "GroupAndKnockout",
    description: "Najpierw grupy, potem najlepsi grają w drabince.",
    icon: Network,
  },
]

export function FormatForm({
  tournament,
  disabled,
  onSaved,
}: {
  tournament: Tournament
  disabled: boolean
  /** Tryb kreatora: przycisk prowadzi do kolejnego kroku, także gdy format się nie zmienił. */
  onSaved?: () => void
}) {
  const changeFormat = useChangeTournamentFormat(tournament.id)
  const [format, setFormat] = useState<TournamentFormat | undefined>(tournament.format ?? undefined)
  const unchanged = format === tournament.format

  function save() {
    if (!format) return
    if (unchanged) return onSaved?.()
    changeFormat.mutate(format, {
      onSuccess: () => {
        toast.success(`Format: ${TOURNAMENT_FORMAT_LABELS[format]}`)
        onSaved?.()
      },
    })
  }

  return (
    <div className="space-y-4">
      <fieldset disabled={disabled} className="grid gap-2 sm:grid-cols-3">
        <legend className="sr-only">Format turnieju</legend>
        {FORMATS.map(({ value, description, icon: Icon }) => (
          <label
            key={value}
            className={cn(
              "has-[:checked]:border-primary has-[:checked]:bg-primary/10 has-[:focus-visible]:ring-ring flex cursor-pointer gap-3 rounded-lg border p-3 transition-colors has-[:focus-visible]:ring-2",
              disabled && "cursor-not-allowed opacity-60",
            )}
          >
            <input
              type="radio"
              name="tournament-format"
              value={value}
              checked={format === value}
              onChange={() => setFormat(value)}
              className="sr-only"
              aria-describedby={`format-${value}-desc`}
            />
            <Icon className="text-primary mt-0.5 size-5 shrink-0" />
            <span>
              <span className="block text-sm font-medium">{TOURNAMENT_FORMAT_LABELS[value]}</span>
              <span id={`format-${value}-desc`} className="text-muted-foreground block text-xs">
                {description}
              </span>
            </span>
          </label>
        ))}
      </fieldset>
      <Button
        onClick={save}
        disabled={disabled || !format || changeFormat.isPending || (unchanged && !onSaved)}
      >
        {changeFormat.isPending ? "Zapisywanie…" : onSaved ? "Zapisz i dalej" : "Zapisz format"}
      </Button>
    </div>
  )
}
