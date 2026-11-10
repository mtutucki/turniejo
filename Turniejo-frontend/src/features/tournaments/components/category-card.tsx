"use client"

import { Pencil, Plus, Trash2 } from "lucide-react"
import Link from "next/link"
import { toast } from "sonner"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { useSchedule } from "@/features/matches/api"
import { SecondStageSection } from "@/features/second-stage/components/second-stage-section"
import { formatBirthDate, plural } from "@/lib/format"
import type { TournamentTeam } from "@/types/teams"
import type {
  SecondStageRange,
  SecondStageSeat,
  TournamentCategory,
  TournamentFormat,
  TournamentGroup,
} from "@/types/tournaments"

import { useDeleteCategory } from "../api"
import { CategoryFormDialog } from "./category-form-dialog"
import { DeleteGroupDialog } from "./delete-group-dialog"
import { GroupFormDialog } from "./group-form-dialog"

function birthRange(category: TournamentCategory) {
  const { minBirthDate: min, maxBirthDate: max } = category
  if (min && max) return `Urodzeni ${formatBirthDate(min)} – ${formatBirthDate(max)}`
  if (min) return `Urodzeni od ${formatBirthDate(min)}`
  if (max) return `Urodzeni do ${formatBirthDate(max)}`
  return "Bez ograniczeń wiekowych"
}

/** "Grupa A", "Grupa B"… — pierwsza litera, której jeszcze nie ma w kategorii. */
function suggestGroupName(groups: TournamentGroup[]) {
  const taken = new Set(groups.map((g) => g.name))
  for (let i = 0; i < 26; i++) {
    const name = `Grupa ${String.fromCharCode(65 + i)}`
    if (!taken.has(name)) return name
  }
  return ""
}

type CategoryCardProps = {
  tournamentId: string
  category: TournamentCategory
  groups: TournamentGroup[]
  /** Drużyny zgłoszone do kategorii (bez odrzuconych/wycofanych) — liczniki przy grupach. */
  teams: TournamentTeam[]
  format: TournamentFormat | null
  canManage: boolean
  /** Dwie fazy grupowe: podział tej kategorii na grupy fazy 2. */
  seats?: SecondStageSeat[]
  /** Dwie fazy grupowe: zakresy miejsc tej kategorii. */
  ranges?: SecondStageRange[]
  /** Terminarz istnieje - podział na fazę 2 jest zamrożony. */
  scheduleLocked?: boolean
}

export function CategoryCard({
  tournamentId,
  category,
  groups: allGroups,
  teams,
  format,
  canManage,
  seats = [],
  ranges = [],
  scheduleLocked = false,
}: CategoryCardProps) {
  const twoStages = format === "TwoGroupStages"
  // Grupy drugiej fazy nie mają przydzielonych drużyn - mają własną sekcję poniżej.
  const groups = allGroups.filter((g) => g.stage !== 2)
  const teamCount = (groupId: string) => teams.filter((t) => t.groupId === groupId).length
  const ungrouped = teams.filter((t) => !t.groupId).length
  const deleteCategory = useDeleteCategory(tournamentId)
  // Co blokuje usunięcie (te same reguły co backend): aktywne drużyny i jakiekolwiek mecze, także
  // odwołane. Pokazujemy to od razu, z linkiem do miejsca, gdzie da się to załatwić.
  const schedule = useSchedule(tournamentId)
  const categoryMatches = (schedule.data ?? []).filter((m) => m.categoryId === category.id)
  const played = categoryMatches.some(
    (m) => m.status === "Live" || m.status === "Paused" || m.status === "Finished",
  )
  const groupMatches = categoryMatches.filter((m) => m.stageType === "Group").length
  const bracketMatches = categoryMatches.length - groupMatches
  const blocked = played || teams.length > 0 || categoryMatches.length > 0
  const nextGroupOrder = Math.max(0, ...allGroups.map((g) => g.order)) + 1

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{category.name}</CardTitle>
        <CardDescription>
          {birthRange(category)} · {plural(teams.length, ["drużyna", "drużyny", "drużyn"])}
        </CardDescription>
        {canManage && (
          <CardAction className="flex gap-1">
            <CategoryFormDialog
              tournamentId={tournamentId}
              category={category}
              nextOrder={category.order}
              trigger={
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Edytuj kategorię ${category.name}`}
                >
                  <Pencil />
                </Button>
              }
            />
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="ghost" size="icon" aria-label={`Usuń kategorię ${category.name}`}>
                  <Trash2 />
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>
                    {blocked
                      ? `Kategorii „${category.name}” nie da się jeszcze usunąć`
                      : `Usunąć kategorię „${category.name}”?`}
                  </AlertDialogTitle>
                  <AlertDialogDescription asChild>
                    {played ? (
                      <p>W tej kategorii są rozegrane mecze, więc zostaje w historii turnieju.</p>
                    ) : blocked ? (
                      <div className="space-y-2">
                        <p>Najpierw:</p>
                        <ul className="list-disc space-y-1 pl-5">
                          {teams.length > 0 && (
                            <li>
                              odrzuć albo wycofaj jej drużyny (
                              {plural(teams.length, ["drużyna", "drużyny", "drużyn"])}).{" "}
                              <Link
                                href={`/tournaments/${tournamentId}/teams`}
                                className="text-foreground underline"
                              >
                                Przejdź do Drużyn
                              </Link>
                            </li>
                          )}
                          {bracketMatches > 0 && (
                            <li>
                              usuń jej drabinkę (ma{" "}
                              {plural(bracketMatches, ["mecz", "mecze", "meczów"])}).{" "}
                              <Link
                                href={`/tournaments/${tournamentId}/bracket?category=${category.id}`}
                                className="text-foreground underline"
                              >
                                Przejdź do Drabinki
                              </Link>
                            </li>
                          )}
                          {groupMatches > 0 && (
                            <li>
                              wyczyść terminarz (
                              {plural(groupMatches, [
                                "mecz grupowy",
                                "mecze grupowe",
                                "meczów grupowych",
                              ])}
                              ).{" "}
                              <Link
                                href={`/tournaments/${tournamentId}/schedule`}
                                className="text-foreground underline"
                              >
                                Przejdź do Terminarza
                              </Link>
                            </li>
                          )}
                        </ul>
                      </div>
                    ) : (
                      <p>
                        Razem z kategorią znikną jej grupy oraz odrzucone i wycofane zgłoszenia.
                      </p>
                    )}
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>{blocked ? "Zamknij" : "Anuluj"}</AlertDialogCancel>
                  {!blocked && (
                    <AlertDialogAction
                      variant="destructive"
                      onClick={() =>
                        deleteCategory.mutate(category.id, {
                          onSuccess: () => toast.success("Kategoria usunięta"),
                        })
                      }
                    >
                      Usuń
                    </AlertDialogAction>
                  )}
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </CardAction>
        )}
      </CardHeader>
      <CardContent className="space-y-3">
        {format === "Knockout" ? (
          // Puchar: pary ustala drabinka, grupy są zbędne.
          <p className="text-muted-foreground text-sm">
            Puchar - pary 1. rundy ustalasz w zakładce{" "}
            <Link href={`/tournaments/${tournamentId}/bracket`} className="text-primary underline">
              Drabinka
            </Link>
            .
          </p>
        ) : !format ? (
          <p className="text-muted-foreground text-sm">
            Wybierz format turnieju - przy formacie z grupami kategoria dostanie od razu „Grupa A”.
          </p>
        ) : (
          <section aria-label={`Grupy w kategorii ${category.name}`} className="space-y-2">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-sm font-medium">{twoStages ? "Faza 1" : "Grupy"}</h3>
              {canManage && (
                <GroupFormDialog
                  tournamentId={tournamentId}
                  categoryId={category.id}
                  categoryName={category.name}
                  nextOrder={nextGroupOrder}
                  suggestedName={suggestGroupName(groups)}
                  trigger={
                    <Button variant="outline" size="sm">
                      <Plus />
                      Dodaj grupę
                    </Button>
                  }
                />
              )}
            </div>
            {groups.length === 0 ? (
              <p className="text-muted-foreground text-sm">Brak grup w tej kategorii.</p>
            ) : (
              <ul className="divide-y rounded-md border">
                {groups.map((group) => (
                  <li key={group.id} className="flex items-center gap-2 py-1 pr-1 pl-3">
                    <span className="min-w-0 flex-1 truncate text-sm font-medium">
                      {group.name}
                    </span>
                    <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                      {plural(teamCount(group.id), ["drużyna", "drużyny", "drużyn"])}
                    </span>
                    {canManage && (
                      <div className="flex shrink-0">
                        <GroupFormDialog
                          tournamentId={tournamentId}
                          categoryId={category.id}
                          categoryName={category.name}
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
                          activeTeams={teamCount(group.id)}
                          matches={categoryMatches.filter((m) => m.groupId === group.id).length}
                        />
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </section>
        )}
        {twoStages && (
          <SecondStageSection
            tournamentId={tournamentId}
            categoryId={category.id}
            categoryName={category.name}
            groups={allGroups}
            firstStage={groups.map((g) => ({
              id: g.id,
              name: g.name,
              order: g.order,
              teams: teamCount(g.id),
            }))}
            seats={seats}
            ranges={ranges}
            matches={categoryMatches}
            canManage={canManage}
            categoryTeams={teams.length}
            locked={scheduleLocked}
          />
        )}
        {format && format !== "Knockout" && !twoStages && groups.length === 1 && (
          <p className="text-muted-foreground text-xs">
            Jedna grupa - wszystkie drużyny kategorii grają każdy z każdym. Drugą grupę dodaj, gdy
            drużyn jest tyle, że wszystkie mecze by się nie zmieściły.
          </p>
        )}
        {format && format !== "Knockout" && groups.length > 0 && ungrouped > 0 && (
          <p className="text-destructive text-xs">
            {plural(ungrouped, ["drużyna nie ma", "drużyny nie mają", "drużyn nie ma"])} grupy -
            przypisz{" "}
            <Link href={`/tournaments/${tournamentId}/teams`} className="underline">
              w Drużynach
            </Link>
            .
          </p>
        )}
      </CardContent>
    </Card>
  )
}
