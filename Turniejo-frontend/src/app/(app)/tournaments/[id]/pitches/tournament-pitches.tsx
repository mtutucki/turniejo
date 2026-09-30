"use client"

import { MapPin, Pencil, Plus } from "lucide-react"

import { EmptyState } from "@/components/layout/empty-state"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { usePitches } from "@/features/matches/api"
import { PitchFormDialog } from "@/features/matches/components/pitch-form-dialog"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"

export function TournamentPitchesView() {
  const { id, isOrganizer } = useTournamentContext()
  const pitches = usePitches(id)
  const activeCount = pitches.data?.filter((p) => p.isActive).length ?? 0

  const addButton = isOrganizer && (
    <PitchFormDialog
      tournamentId={id}
      suggestedName={`Boisko ${(pitches.data?.length ?? 0) + 1}`}
      trigger={
        <Button>
          <Plus />
          Dodaj boisko
        </Button>
      }
    />
  )

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">Boiska</h2>
          <p className="text-muted-foreground text-sm">
            {pitches.data?.length
              ? `${plural(activeCount, ["aktywne boisko", "aktywne boiska", "aktywnych boisk"])} — na nich generator ustawia mecze.`
              : "Miejsca, na których rozgrywane są mecze."}
          </p>
        </div>
        {!!pitches.data?.length && addButton}
      </div>

      {pitches.isPending ? (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-24 rounded-xl" />
          ))}
        </div>
      ) : pitches.data?.length ? (
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {pitches.data.map((pitch) => (
            <li key={pitch.id}>
              <Card className={cn("h-full py-4", !pitch.isActive && "opacity-60")}>
                <CardContent className="flex items-start gap-3 px-4">
                  <MapPin className="text-primary mt-0.5 size-4 shrink-0" />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="truncate font-medium">{pitch.name}</p>
                      {!pitch.isActive && <Badge variant="secondary">Nieaktywne</Badge>}
                    </div>
                    {pitch.description && (
                      <p className="text-muted-foreground mt-1 line-clamp-2 text-sm">
                        {pitch.description}
                      </p>
                    )}
                  </div>
                  {isOrganizer && (
                    <PitchFormDialog
                      tournamentId={id}
                      pitch={pitch}
                      trigger={
                        <Button
                          size="icon"
                          variant="ghost"
                          className="-mt-1 -mr-2"
                          aria-label={`Edytuj: ${pitch.name}`}
                        >
                          <Pencil />
                        </Button>
                      }
                    />
                  )}
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          icon={MapPin}
          title="Brak boisk"
          description={
            isOrganizer
              ? "Dodaj co najmniej jedno boisko — bez niego nie wygenerujesz terminarza."
              : "Organizator nie dodał jeszcze boisk."
          }
          action={addButton || undefined}
        />
      )}
    </section>
  )
}
