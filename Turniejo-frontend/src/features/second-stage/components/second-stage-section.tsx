"use client"

import { Pencil, Plus, Shuffle } from "lucide-react"

import { Button } from "@/components/ui/button"
import { DeleteGroupDialog } from "@/features/tournaments/components/delete-group-dialog"
import { GroupFormDialog } from "@/features/tournaments/components/group-form-dialog"
import { plural } from "@/lib/format"
import type { Match } from "@/types/matches"
import type { SecondStageSeat, TournamentGroup } from "@/types/tournaments"

import {
  type FirstStageGroup,
  missingPlaces,
  placesByPosition,
  suggestSecondStageGroupName,
} from "../split"
import { SecondStageSplitDialog } from "./second-stage-split-dialog"

type SecondStageSectionProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  /** Wszystkie grupy kategorii (obie fazy) - do nazw i propozycji nazwy nowej grupy. */
  groups: TournamentGroup[]
  firstStage: FirstStageGroup[]
  seats: SecondStageSeat[]
  matches: Match[]
  canManage: boolean
  /** Zgłoszone drużyny kategorii, także bez grupy. */
  categoryTeams: number
  /** Terminarz istnieje: podział jest zamrożony (backend odmawia zmiany). */
  locked: boolean
}

/** Karta kategorii w formacie „Dwie fazy grupowe”: grupy fazy 2 i skąd przychodzą do nich drużyny. */
export function SecondStageSection({
  tournamentId,
  categoryId,
  categoryName,
  groups,
  firstStage,
  seats,
  matches,
  canManage,
  categoryTeams,
  locked,
}: SecondStageSectionProps) {
  const secondStage = groups.filter((g) => g.stage === 2)
  const teamsIn = (groupId: string) => firstStage.find((g) => g.id === groupId)?.teams ?? 0
  const missing = missingPlaces(seats, teamsIn, groups)
  const nextOrder = Math.max(0, ...groups.map((g) => g.order)) + 1

  return (
    <section aria-label={`Faza 2 w kategorii ${categoryName}`} className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Faza 2</h3>
        {canManage && (
          <div className="flex flex-wrap gap-2">
            {secondStage.length > 0 && !locked && (
              <SecondStageSplitDialog
                tournamentId={tournamentId}
                categoryId={categoryId}
                categoryName={categoryName}
                firstStage={firstStage}
                secondStage={secondStage}
                seats={seats}
                categoryTeams={categoryTeams}
                trigger={
                  <Button variant="outline" size="sm">
                    <Shuffle />
                    Ustaw podział
                  </Button>
                }
              />
            )}
            <GroupFormDialog
              tournamentId={tournamentId}
              categoryId={categoryId}
              categoryName={categoryName}
              nextOrder={nextOrder}
              suggestedName={suggestSecondStageGroupName(groups)}
              stage={2}
              trigger={
                <Button variant="outline" size="sm">
                  <Plus />
                  Dodaj grupę
                </Button>
              }
            />
          </div>
        )}
      </div>
      {secondStage.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Dodaj grupy drugiej fazy, np. Grupa I dla miejsc 1-2 i Grupa II dla miejsc 3-4.
        </p>
      ) : (
        <ul className="divide-y rounded-md border">
          {secondStage.map((group) => {
            const places = seats.filter((s) => s.groupId === group.id)
            return (
              <li key={group.id} className="flex items-start gap-2 py-1.5 pr-1 pl-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">
                    {group.name}
                    {places.length > 0 && (
                      <span className="text-muted-foreground font-normal">
                        {" "}
                        · {plural(places.length, ["drużyna", "drużyny", "drużyn"])}
                      </span>
                    )}
                  </p>
                  {places.length > 0 ? (
                    <ul className="text-muted-foreground text-xs">
                      {placesByPosition(places, groups).map((line) => (
                        <li key={line}>{line}</li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-muted-foreground text-xs">Bez miejsc - ustaw podział.</p>
                  )}
                </div>
                {canManage && (
                  <div className="flex shrink-0">
                    <GroupFormDialog
                      tournamentId={tournamentId}
                      categoryId={categoryId}
                      categoryName={categoryName}
                      group={group}
                      nextOrder={group.order}
                      trigger={
                        <Button variant="ghost" size="icon" aria-label={`Edytuj ${group.name}`}>
                          <Pencil />
                        </Button>
                      }
                    />
                    <DeleteGroupDialog
                      tournamentId={tournamentId}
                      group={group}
                      activeTeams={0}
                      matches={matches.filter((m) => m.groupId === group.id).length}
                    />
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
      {missing.length > 0 && (
        <p className="text-destructive text-xs">
          W podziale {missing.length === 1 ? "jest miejsce" : "są miejsca"}, których nie ma w
          tabeli: {missing.join(", ")}.{" "}
          {locked ? "Wyczyść terminarz i popraw podział." : "Popraw podział."}
        </p>
      )}
      {locked && secondStage.length > 0 && (
        <p className="text-muted-foreground text-xs">
          Podział zmienisz po wyczyszczeniu terminarza.
        </p>
      )}
    </section>
  )
}
