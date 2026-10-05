"use client"

import { GitBranch } from "lucide-react"
import { useState } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { BracketBoard } from "@/features/matches/components/bracket-board"
import { cn } from "@/lib/utils"

import { usePublicBracket, usePublicSchedule } from "../api"
import { usePublicContext } from "../public-context"

function CategoryBracket({ categoryId }: { categoryId: string }) {
  const { slug, tournament, lookups } = usePublicContext()
  const bracket = usePublicBracket(slug, categoryId)
  const schedule = usePublicSchedule(slug, tournament.settings.showSchedule)

  if (bracket.isPending) return <Skeleton className="h-64 w-full rounded-xl" />
  if (!bracket.data) {
    return (
      <EmptyState
        icon={GitBranch}
        title="Drabinka jeszcze nie powstała"
        description="Pojawi się po zakończeniu fazy grupowej."
      />
    )
  }

  return (
    <BracketBoard
      bracket={bracket.data}
      matches={schedule.data ?? []}
      lookups={{
        teamName: (id) => lookups.teamName(id),
        groupName: lookups.groupName,
        pitchName: lookups.pitchName,
      }}
      // Terminy tylko przy widocznym terminarzu; puchar przed terminarzem ma w bazie zaślepki.
      showTimes={
        tournament.settings.showSchedule &&
        !(
          tournament.format === "Knockout" &&
          (tournament.status === "Draft" ||
            tournament.status === "RegistrationOpen" ||
            tournament.status === "RegistrationClosed")
        )
      }
    />
  )
}

export function BracketView() {
  const { tournament } = usePublicContext()
  const [categoryId, setCategoryId] = useState(tournament.categories[0]?.id)

  if (!categoryId) return <EmptyState icon={GitBranch} title="Brak kategorii" />

  return (
    <div className="space-y-4">
      {tournament.categories.length > 1 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Kategoria">
          {tournament.categories.map((c) => (
            <button
              key={c.id}
              type="button"
              aria-pressed={c.id === categoryId}
              onClick={() => setCategoryId(c.id)}
              className={cn(
                "h-9 rounded-full border px-4 text-sm font-medium",
                c.id === categoryId
                  ? "bg-primary text-primary-foreground border-primary"
                  : "bg-card text-muted-foreground",
              )}
            >
              {c.name}
            </button>
          ))}
        </div>
      )}
      <CategoryBracket key={categoryId} categoryId={categoryId} />
    </div>
  )
}
