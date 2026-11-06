"use client"

import { PLAYER_RANKINGS, RankingCard } from "@/features/matches/components/player-rankings"

import { type RankingKind, usePublicRanking } from "../api"
import { usePublicContext } from "../public-context"

const PUBLIC_KIND = { scorers: "top-scorers", assists: "top-assists", mvp: "mvp-ranking" } as const

function PublicRankingCard({ kind, ...props }: (typeof PLAYER_RANKINGS)[number]) {
  const { slug, teams, lookups } = usePublicContext()
  const ranking = usePublicRanking(slug, PUBLIC_KIND[kind] satisfies RankingKind)
  return (
    <RankingCard
      {...props}
      ranking={ranking}
      // Nazwa drużyny tylko gdy portal pokazuje drużyny (lookups zwraca wtedy prawdziwą nazwę).
      teamName={(id) => (teams.length ? lookups.teamName(id) : null)}
    />
  )
}

export function StatsView() {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {PLAYER_RANKINGS.map((r) => (
        <PublicRankingCard key={r.kind} {...r} />
      ))}
    </div>
  )
}
