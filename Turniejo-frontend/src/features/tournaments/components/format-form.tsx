"use client"

import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentFormat,
} from "@/types/tournaments"

import { useChangeTournamentFormat } from "../api"

const FORMATS = Object.keys(TOURNAMENT_FORMAT_LABELS) as TournamentFormat[]

export function FormatForm({
  tournament,
  disabled,
}: {
  tournament: Tournament
  disabled: boolean
}) {
  const changeFormat = useChangeTournamentFormat(tournament.id)
  const [format, setFormat] = useState<TournamentFormat | undefined>(tournament.format ?? undefined)

  function save() {
    if (!format) return
    changeFormat.mutate(format, {
      onSuccess: () => toast.success(`Format: ${TOURNAMENT_FORMAT_LABELS[format]}`),
    })
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Select
        value={format}
        onValueChange={(value) => setFormat(value as TournamentFormat)}
        disabled={disabled}
      >
        <SelectTrigger className="w-56" aria-label="Format turnieju">
          <SelectValue placeholder="Wybierz format" />
        </SelectTrigger>
        <SelectContent>
          {FORMATS.map((value) => (
            <SelectItem key={value} value={value}>
              {TOURNAMENT_FORMAT_LABELS[value]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Button
        onClick={save}
        disabled={disabled || !format || format === tournament.format || changeFormat.isPending}
      >
        {changeFormat.isPending ? "Zapisywanie…" : "Zapisz format"}
      </Button>
    </div>
  )
}
