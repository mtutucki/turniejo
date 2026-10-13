"use client"

import { Check, ChevronRight } from "lucide-react"
import Link from "next/link"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import { TOURNAMENT_FORMAT_LABELS, type Tournament } from "@/types/tournaments"

import { SETUP_CHECKLIST, type SetupChecklistKey, useSetupProgress } from "../use-setup-progress"

/**
 * Lista "co jeszcze trzeba zrobić, żeby turniej mógł ruszyć" na przeglądzie. Każda pozycja otwiera
 * właściwy krok kreatora, więc organizator nie musi wiedzieć, co od czego zależy.
 */
export function SetupProgressCard({ tournament }: { tournament: Tournament }) {
  const progress = useSetupProgress(tournament.id, tournament)
  if (progress.isPending) return null

  const base = `/tournaments/${tournament.id}/setup`
  const items: Record<SetupChecklistKey, { title: string; detail: string }> = {
    format: {
      title: "Format rozgrywek",
      detail: tournament.format ? TOURNAMENT_FORMAT_LABELS[tournament.format] : "do wybrania",
    },
    categories: {
      title: "Kategorie",
      detail: progress.categoryCount
        ? plural(progress.categoryCount, ["kategoria", "kategorie", "kategorii"])
        : "dodaj co najmniej jedną",
    },
    pitches: {
      title: "Boiska",
      detail: progress.activePitches
        ? plural(progress.activePitches, ["aktywne boisko", "aktywne boiska", "aktywnych boisk"])
        : "dodaj co najmniej jedno",
    },
    teams: {
      title: "Drużyny",
      detail:
        `${progress.acceptedTeams.length} zaakceptowanych` +
        (progress.pendingTeams.length ? ` · ${progress.pendingTeams.length} czeka` : "") +
        (progress.done.teams ? "" : " — potrzeba co najmniej 2"),
    },
    schedule: { title: "Terminarz", detail: "godziny i boiska meczów" },
  }

  return (
    <Card className="border-primary/40">
      <CardHeader>
        <CardTitle className="text-base">Przygotowanie turnieju</CardTitle>
        <CardDescription>
          Gotowe {progress.doneCount} z {SETUP_CHECKLIST.length} kroków. Kreator poprowadzi Cię po
          kolei przez to, czego brakuje.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <ol className="divide-y">
          {SETUP_CHECKLIST.map((key) => {
            const done = progress.done[key]
            return (
              <li key={key}>
                <Link
                  href={`${base}?step=${key}`}
                  className="hover:bg-accent/50 focus-visible:ring-ring -mx-2 flex items-center gap-3 rounded-md px-2 py-2 text-sm outline-none focus-visible:ring-2"
                >
                  <span
                    className={cn(
                      "flex size-5 shrink-0 items-center justify-center rounded-full border",
                      done && "bg-primary text-primary-foreground border-primary",
                    )}
                  >
                    {done && <Check className="size-3" aria-label="Zrobione" />}
                  </span>
                  <span className={cn("font-medium", done && "text-muted-foreground")}>
                    {items[key].title}
                  </span>
                  <span className="text-muted-foreground min-w-0 flex-1 truncate text-right text-xs">
                    {items[key].detail}
                  </span>
                  <ChevronRight className="text-muted-foreground size-4 shrink-0" />
                </Link>
              </li>
            )
          })}
        </ol>
        <Button asChild>
          <Link href={base}>Kontynuuj konfigurację</Link>
        </Button>
      </CardContent>
    </Card>
  )
}
