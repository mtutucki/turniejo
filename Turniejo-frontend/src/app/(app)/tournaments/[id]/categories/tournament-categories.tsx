"use client"

import { Layers, Plus } from "lucide-react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useSecondStage } from "@/features/second-stage/api"
import { useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups, useTieBreakerRules } from "@/features/tournaments/api"
import { CategoryCard } from "@/features/tournaments/components/category-card"
import { CategoryFormDialog } from "@/features/tournaments/components/category-form-dialog"
import { TieBreakerEditor } from "@/features/tournaments/components/tie-breaker-editor"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { hasGroupStage, statusAtLeast } from "@/types/tournaments"

export function TournamentCategoriesView({
  embedded = false,
}: {
  /** W kreatorze turnieju: same kategorie, kolejność w tabeli ma tam własny krok. */
  embedded?: boolean
}) {
  const { id, isOrganizer, tournament } = useTournamentContext()
  const categories = useCategories(id)
  const groups = useGroups(id)
  const teams = useTournamentTeams(id)
  const format = tournament.data?.format ?? null
  const knockout = format === "Knockout"
  const tieBreakerRules = useTieBreakerRules(id)
  const secondStage = useSecondStage(id, format === "TwoGroupStages")
  const nextOrder = Math.max(0, ...(categories.data ?? []).map((c) => c.order)) + 1

  const addCategoryButton = isOrganizer && (
    <CategoryFormDialog
      tournamentId={id}
      nextOrder={nextOrder}
      // Jak DefaultGroupProvisioner: grupy powstają tylko w formacie z grupami, przed terminarzem.
      withGroups={
        hasGroupStage(format) &&
        !!tournament.data &&
        !statusAtLeast(tournament.data.status, "ScheduleGenerated")
      }
      trigger={
        <Button>
          <Plus />
          Dodaj kategorię
        </Button>
      }
    />
  )

  return (
    <div className="space-y-8">
      <section className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold">
              {knockout ? "Kategorie" : "Kategorie i grupy"}
            </h2>
            <p className="text-muted-foreground max-w-2xl text-sm">
              <span className="text-foreground font-medium">Kategoria</span> to osobne rozgrywki
              (np. U10, U12) - drużyny z różnych kategorii nie grają ze sobą.
              {!knockout && (
                <>
                  {" "}
                  <span className="text-foreground font-medium">Grupa</span> to pula w kategorii, w
                  której gra się każdy z każdym. Przy jednej grupie nic nie musisz ustawiać - „Grupa
                  A” powstaje sama, a drużyny trafiają do niej automatycznie.
                </>
              )}
            </p>
          </div>
          {categories.data && categories.data.length > 0 && addCategoryButton}
        </div>

        {categories.isPending || groups.isPending ? (
          <div className="grid gap-4 xl:grid-cols-2">
            {[0, 1].map((i) => (
              <Skeleton key={i} className="h-40 rounded-xl" />
            ))}
          </div>
        ) : categories.data?.length ? (
          <div className="grid gap-4 xl:grid-cols-2">
            {categories.data.map((category) => (
              <CategoryCard
                key={category.id}
                tournamentId={id}
                category={category}
                groups={(groups.data ?? []).filter((g) => g.categoryId === category.id)}
                teams={(teams.data ?? []).filter(
                  (t) =>
                    t.categoryId === category.id &&
                    t.registrationStatus !== "Rejected" &&
                    t.registrationStatus !== "Withdrawn",
                )}
                format={format}
                canManage={isOrganizer}
                seats={(secondStage.data?.seats ?? []).filter((s) => s.categoryId === category.id)}
                ranges={(secondStage.data?.ranges ?? []).filter(
                  (r) => r.categoryId === category.id,
                )}
                // Zanim drużyny się zgłoszą, ich liczbę wyznacza limit turnieju (dzielony na kategorie).
                teamLimitShare={Math.ceil(
                  (tournament.data?.teamLimit ?? 0) / Math.max(1, categories.data?.length ?? 1),
                )}
                scheduleLocked={
                  !!tournament.data && statusAtLeast(tournament.data.status, "ScheduleGenerated")
                }
              />
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Layers}
            title="Brak kategorii"
            description="Dodaj kategorię (np. U10 albo „Open”). Nawet przy jednym roczniku potrzebna jest jedna kategoria."
            action={addCategoryButton || undefined}
          />
        )}
      </section>

      {!embedded && (
        <Card className="max-w-2xl">
          <CardHeader>
            <CardTitle className="text-base">
              Kolejność w tabeli przy równej liczbie punktów
            </CardTitle>
            <CardDescription>
              Reguły stosowane po kolei - przeciągnij, żeby zmienić priorytet.
            </CardDescription>
          </CardHeader>
          <CardContent>
            {tieBreakerRules.data ? (
              <TieBreakerEditor
                // Po zapisie i odświeżeniu danych stan edytora startuje od nowa z zapisanej kolejności.
                key={tieBreakerRules.data.map((r) => r.type).join()}
                tournamentId={id}
                savedRules={tieBreakerRules.data}
                canManage={isOrganizer}
              />
            ) : (
              <Skeleton className="h-40" />
            )}
          </CardContent>
        </Card>
      )}
    </div>
  )
}
