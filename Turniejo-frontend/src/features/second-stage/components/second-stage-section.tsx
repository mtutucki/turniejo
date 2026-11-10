"use client"

import { ListOrdered, Pencil, Plus, Shuffle } from "lucide-react"

import { Button } from "@/components/ui/button"
import { DeleteGroupDialog } from "@/features/tournaments/components/delete-group-dialog"
import { GroupFormDialog } from "@/features/tournaments/components/group-form-dialog"
import { plural } from "@/lib/format"
import type { Match } from "@/types/matches"
import type { SecondStageRange, SecondStageSeat, TournamentGroup } from "@/types/tournaments"

import {
  type FirstStageGroup,
  largestGroupEstimate,
  missingPlaces,
  placesByPosition,
  proposeRanges,
  rangeLabel,
  suggestSecondStageGroupName,
} from "../split"
import { SecondStageRangesDialog } from "./second-stage-ranges-dialog"
import { SecondStageSplitDialog } from "./second-stage-split-dialog"

type SecondStageSectionProps = {
  tournamentId: string
  categoryId: string
  categoryName: string
  /** Wszystkie grupy kategorii (obie fazy) - do nazw i propozycji nazwy nowej grupy. */
  groups: TournamentGroup[]
  firstStage: FirstStageGroup[]
  seats: SecondStageSeat[]
  /** Zakresy miejsc tej kategorii (Grupa I: miejsca 1-2). */
  ranges: SecondStageRange[]
  matches: Match[]
  canManage: boolean
  /** Zgłoszone drużyny kategorii (także bez grupy), a bez zgłoszeń część limitu turnieju. */
  categoryTeams: number
  /** categoryTeams pochodzi z limitu drużyn turnieju, a nie ze zgłoszeń. */
  teamsFromLimit?: boolean
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
  ranges,
  matches,
  canManage,
  categoryTeams,
  teamsFromLimit = false,
  locked,
}: SecondStageSectionProps) {
  const secondStage = groups.filter((g) => g.stage === 2)
  const teamsIn = (groupId: string) => firstStage.find((g) => g.id === groupId)?.teams ?? 0
  // Co pokazać: po wygenerowaniu terminarza konkretne miejsca, wcześniej zakresy, pojedyncze miejsca
  // (tryb zaawansowany) albo gotowy podział, który generator zastosuje sam.
  const mode =
    locked && seats.length > 0
      ? "seats"
      : ranges.length > 0
        ? "ranges"
        : seats.length > 0
          ? "seats"
          : "default"
  const shownRanges =
    mode === "ranges"
      ? ranges
      : proposeRanges(
          largestGroupEstimate(firstStage, categoryTeams),
          [...secondStage].sort((a, b) => a.order - b.order).map((g) => g.id),
        )
  const missing = mode === "seats" ? missingPlaces(seats, teamsIn, groups) : []
  const nextOrder = Math.max(0, ...groups.map((g) => g.order)) + 1

  return (
    <section aria-label={`Faza 2 w kategorii ${categoryName}`} className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-medium">Faza 2</h3>
        {canManage && (
          <div className="flex flex-wrap gap-2">
            {secondStage.length > 0 && !locked && (
              <>
                <SecondStageRangesDialog
                  tournamentId={tournamentId}
                  categoryId={categoryId}
                  categoryName={categoryName}
                  firstStage={firstStage}
                  secondStage={secondStage}
                  ranges={ranges}
                  categoryTeams={categoryTeams}
                  teamsFromLimit={teamsFromLimit}
                  trigger={
                    <Button variant="outline" size="sm">
                      <ListOrdered />
                      Ustaw podział
                    </Button>
                  }
                />
                <SecondStageSplitDialog
                  tournamentId={tournamentId}
                  categoryId={categoryId}
                  categoryName={categoryName}
                  firstStage={firstStage}
                  secondStage={secondStage}
                  seats={seats}
                  categoryTeams={categoryTeams}
                  teamsFromLimit={teamsFromLimit}
                  trigger={
                    <Button variant="ghost" size="sm">
                      <Shuffle />
                      Pojedyncze miejsca
                    </Button>
                  }
                />
              </>
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
            const places = mode === "seats" ? seats.filter((s) => s.groupId === group.id) : []
            const range = shownRanges.find((r) => r.groupId === group.id)
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
                  {mode !== "seats" ? (
                    <p className="text-muted-foreground text-xs">
                      {range
                        ? `${mode === "default" ? "Domyślnie: " : ""}${capitalize(rangeLabel(range))} z każdej grupy`
                        : "Bez miejsc - ustaw podział."}
                    </p>
                  ) : places.length > 0 ? (
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
      {mode === "default" && secondStage.length > 0 && !locked && (
        <p className="text-muted-foreground text-xs">
          Bez zmian terminarz użyje tego podziału, a dokładne miejsca policzy z drużyn w grupach.
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

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1)
