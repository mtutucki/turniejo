"use client"

import { Baby, ChevronRight } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { panelKeys, useChildTeams, useMyChildren } from "@/features/panels/api"
import { TeamLogo } from "@/features/teams/components/team-logo"
import { prefetchHandlers, usePrefetch } from "@/hooks/use-prefetch"
import { formatBirthDate } from "@/lib/format"
import { type MyChild, RELATIONSHIP_LABELS } from "@/types/roles-panels"

function ChildCard({ child }: { child: MyChild }) {
  const teams = useChildTeams(child.playerId)
  const name = `${child.firstName} ${child.lastName}`
  const prefetch = usePrefetch()
  return (
    <Link
      href={`/parent/players/${child.playerId}/schedule`}
      {...prefetchHandlers(() =>
        prefetch({
          queryKey: panelKeys.child(child.playerId, "schedule"),
          path: `/api/parent/players/${child.playerId}/schedule`,
        }),
      )}
      className="focus-visible:ring-ring block rounded-xl outline-none focus-visible:ring-2"
    >
      <Card className="hover:border-primary/50 h-full py-4 transition-colors">
        <CardContent className="flex items-center gap-4 px-4">
          <TeamLogo name={name} logoUrl={child.photoUrl} size={44} className="rounded-full" />
          <div className="min-w-0 flex-1 space-y-1">
            <p className="truncate font-semibold">{name}</p>
            <p className="text-muted-foreground text-xs">
              ur. {formatBirthDate(child.birthDate)} · {RELATIONSHIP_LABELS[child.relationshipType]}
            </p>
            <p className="text-muted-foreground truncate text-xs">
              {teams.data?.length
                ? teams.data.map((t) => `${t.teamName} (${t.tournamentName})`).join(", ")
                : teams.isPending
                  ? "…"
                  : "Bez drużyny"}
            </p>
          </div>
          <ChevronRight className="text-muted-foreground size-5 shrink-0" />
        </CardContent>
      </Card>
    </Link>
  )
}

export function MyChildrenView() {
  const children = useMyChildren()
  return (
    <div>
      <PageHeader
        title="Moje dzieci"
        description="Terminarz, wyniki i tabela drużyn Twoich dzieci."
      />
      {children.isPending ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-24 rounded-xl" />
          ))}
        </div>
      ) : children.data?.length ? (
        <ul className="grid gap-3 sm:grid-cols-2">
          {children.data.map((child) => (
            <li key={child.playerId}>
              <ChildCard child={child} />
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          icon={Baby}
          title="Nie masz przypisanych dzieci"
          description="Organizator albo trener drużyny przypisze Cię jako opiekuna zawodnika."
        />
      )}
    </div>
  )
}
