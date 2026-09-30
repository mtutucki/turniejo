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
import { formatBirthDate, plural } from "@/lib/format"
import type { TournamentTeam } from "@/types/teams"
import type { TournamentCategory, TournamentFormat, TournamentGroup } from "@/types/tournaments"

import { useDeleteCategory } from "../api"
import { CategoryFormDialog } from "./category-form-dialog"
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
}

export function CategoryCard({
  tournamentId,
  category,
  groups,
  teams,
  format,
  canManage,
}: CategoryCardProps) {
  const teamCount = (groupId: string) => teams.filter((t) => t.groupId === groupId).length
  const ungrouped = teams.filter((t) => !t.groupId).length
  const deleteCategory = useDeleteCategory(tournamentId)
  const nextGroupOrder = Math.max(0, ...groups.map((g) => g.order)) + 1

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
                  <AlertDialogTitle>Usunąć kategorię „{category.name}”?</AlertDialogTitle>
                  <AlertDialogDescription>
                    Razem z kategorią zostaną usunięte jej grupy. Nie da się usunąć kategorii, do
                    której są już zapisane drużyny lub zaplanowane mecze.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Anuluj</AlertDialogCancel>
                  <AlertDialogAction
                    className="bg-destructive hover:bg-destructive/90 text-white"
                    onClick={() =>
                      deleteCategory.mutate(category.id, {
                        onSuccess: () => toast.success("Kategoria usunięta"),
                      })
                    }
                  >
                    Usuń
                  </AlertDialogAction>
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
            Puchar — pary 1. rundy ustalasz w zakładce{" "}
            <Link href={`/tournaments/${tournamentId}/bracket`} className="text-primary underline">
              Drabinka
            </Link>
            .
          </p>
        ) : !format ? (
          <p className="text-muted-foreground text-sm">
            Wybierz format turnieju — przy formacie z grupami kategoria dostanie od razu „Grupa A”.
          </p>
        ) : groups.length === 0 ? (
          <p className="text-muted-foreground text-sm">Brak grup w tej kategorii.</p>
        ) : (
          <ul className="flex flex-wrap gap-2">
            {groups.map((group) => (
              <li key={group.id}>
                {canManage ? (
                  <GroupFormDialog
                    tournamentId={tournamentId}
                    categoryId={category.id}
                    categoryName={category.name}
                    group={group}
                    nextOrder={group.order}
                    trigger={
                      <button
                        type="button"
                        className="bg-secondary hover:bg-accent focus-visible:ring-ring flex items-center gap-2 rounded-md px-3 py-1.5 text-sm outline-none focus-visible:ring-2"
                        aria-label={`Edytuj ${group.name}`}
                      >
                        {group.name}
                        <span className="text-muted-foreground font-mono text-xs tabular-nums">
                          {teamCount(group.id)}
                        </span>
                        <Pencil className="text-muted-foreground size-3" />
                      </button>
                    }
                  />
                ) : (
                  <span className="bg-secondary rounded-md px-3 py-1.5 text-sm">
                    {group.name} ·{" "}
                    <span className="font-mono tabular-nums">{teamCount(group.id)}</span>
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}
        {format && format !== "Knockout" && groups.length === 1 && (
          <p className="text-muted-foreground text-xs">
            Jedna grupa — wszystkie drużyny kategorii grają każdy z każdym. Drugą grupę dodaj, gdy
            drużyn jest tyle, że wszystkie mecze by się nie zmieściły.
          </p>
        )}
        {format && format !== "Knockout" && groups.length > 0 && ungrouped > 0 && (
          <p className="text-destructive text-xs">
            {plural(ungrouped, ["drużyna nie ma", "drużyny nie mają", "drużyn nie ma"])} grupy —
            przypisz{" "}
            <Link href={`/tournaments/${tournamentId}/teams`} className="underline">
              w Drużynach
            </Link>
            .
          </p>
        )}
        {canManage && format && format !== "Knockout" && (
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
      </CardContent>
    </Card>
  )
}
