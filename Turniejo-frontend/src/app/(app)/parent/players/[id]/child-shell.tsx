"use client"

import { ArrowLeft, ShieldAlert } from "lucide-react"
import Link from "next/link"
import { useParams } from "next/navigation"
import type { ReactNode } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { useChildTeams, useMyChildren } from "@/features/panels/api"
import { EditChildDialog } from "@/features/panels/components/edit-child-dialog"
import { SectionTabs } from "@/features/panels/components/section-tabs"

const TABS = [
  { segment: "schedule", label: "Terminarz" },
  { segment: "results", label: "Wyniki" },
  { segment: "standing", label: "Tabela" },
]

export function ChildShell({ children }: { children: ReactNode }) {
  const { id } = useParams<{ id: string }>()
  const myChildren = useMyChildren()
  const teams = useChildTeams(id)
  const child = myChildren.data?.find((c) => c.playerId === id)

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2">
      <Link href="/parent/players">
        <ArrowLeft />
        Moje dzieci
      </Link>
    </Button>
  )

  if (myChildren.isPending) return <Skeleton className="h-64 w-full rounded-xl" />
  // Cudze dziecko: backend i tak zwraca 403 na każdą podstronę — nie pokazujemy nawet zakładek.
  if (!child) {
    return (
      <div className="space-y-4">
        {back}
        <EmptyState
          icon={ShieldAlert}
          title="Brak dostępu do tego zawodnika"
          description="Widzisz tylko dzieci, do których organizator albo trener przypisał Cię jako opiekuna."
        />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <header className="space-y-3">
        {back}
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              {child.firstName} {child.lastName}
            </h1>
            <p className="text-muted-foreground text-sm">
              {teams.data
                ?.map(
                  (t) =>
                    `${t.teamName}${t.shirtNumber ? ` · nr ${t.shirtNumber}` : ""} (${t.tournamentName})`,
                )
                .join(", ") || "Bez drużyny"}
            </p>
          </div>
          {child.canEdit && <EditChildDialog child={child} />}
        </div>
        <SectionTabs base={`/parent/players/${id}`} tabs={TABS} />
      </header>
      {children}
    </div>
  )
}
