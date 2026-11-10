"use client"

import { ChevronRight, Shield } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { TeamLogo } from "@/features/teams/components/team-logo"

import { usePublicContext } from "../public-context"

export function TeamsView() {
  const { slug, tournament, teams } = usePublicContext()

  if (teams.length === 0) return <EmptyState icon={Shield} title="Brak drużyn" />

  const sections = tournament.categories
    .map((category) => ({
      category,
      groups: [
        ...tournament.groups
          .filter((g) => g.categoryId === category.id)
          .map((g) => ({
            key: g.id,
            title: g.name,
            teams: teams.filter((t) => t.groupId === g.id),
          })),
        {
          key: `${category.id}-none`,
          title: "Bez grupy",
          teams: teams.filter((t) => t.categoryId === category.id && !t.groupId),
        },
      ].filter((g) => g.teams.length > 0),
    }))
    .filter((s) => s.groups.length > 0)

  return (
    <div className="space-y-8">
      {sections.map(({ category, groups }) => (
        <section key={category.id} className="space-y-3">
          {tournament.categories.length > 1 && (
            <h2 className="text-muted-foreground text-xs font-semibold tracking-wide uppercase">
              {category.name}
            </h2>
          )}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {groups.map((group) => (
              <section key={group.key} className="bg-card rounded-xl border p-4">
                <h3 className="mb-3 font-semibold">{group.title}</h3>
                <ul className="space-y-2">
                  {group.teams.map((team) => (
                    <li key={team.tournamentTeamId}>
                      {/* Lista drużyn istnieje tylko przy „Pokazuj drużyny”, więc strona drużyny też. */}
                      <Link
                        href={`/t/${slug}/teams/${team.tournamentTeamId}`}
                        aria-label={`${team.name}: mecze, wyniki i skład`}
                        className="hover:bg-accent focus-visible:ring-ring -mx-2 flex items-center gap-3 rounded-lg px-2 py-1 outline-none focus-visible:ring-2"
                      >
                        <TeamLogo name={team.name} logoUrl={team.logoUrl} size={32} />
                        <span className="min-w-0 flex-1 truncate">{team.name}</span>
                        <ChevronRight className="text-muted-foreground size-4 shrink-0" />
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
