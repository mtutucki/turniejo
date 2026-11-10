"use client"

import { Trophy } from "lucide-react"
import Link from "next/link"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { formatDateRange, plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { TeamHistory, TeamRecordSummary } from "@/types/teams"

import { useTeamHistory } from "../api"
import { tournamentResult, winRate } from "../team-history"
import { OutcomeBadge, Tile } from "./team-stats"

const recordText = (r: TeamRecordSummary) => `${r.wins}-${r.draws}-${r.losses}`
const goalsText = (r: TeamRecordSummary) => `${r.goalsFor}:${r.goalsAgainst}`

function Heading({ children }: { children: string }) {
  return (
    <h3 className="text-muted-foreground mb-2 text-xs font-semibold tracking-wide uppercase">
      {children}
    </h3>
  )
}

/** Drużyna z bazy we wszystkich turniejach: bilans, wyniki turniejów, zawodnicy i rywale. */
export function TeamHistorySection({ teamId }: { teamId: string }) {
  const history = useTeamHistory(teamId)
  const data = history.data

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Statystyki we wszystkich turniejach</CardTitle>
        <CardDescription>
          {history.isPending
            ? "Wczytywanie…"
            : history.isError
              ? "Nie udało się wczytać statystyk."
              : !data?.tournamentCount
                ? "Drużyna nie grała jeszcze w żadnym turnieju."
                : `${plural(data.tournamentCount, ["turniej", "turnieje", "turniejów"])}, liczą się zakończone mecze.`}
        </CardDescription>
      </CardHeader>
      {history.isPending ? (
        <CardContent>
          <Skeleton className="h-24 w-full" />
        </CardContent>
      ) : (
        data &&
        data.tournamentCount > 0 && (
          <CardContent className="space-y-6">
            <Summary data={data} />
            <Tournaments data={data} />
            <div className="grid gap-6 xl:grid-cols-2">
              <Players data={data} />
              <Opponents data={data} />
            </div>
          </CardContent>
        )
      )}
    </Card>
  )
}

function Summary({ data }: { data: TeamHistory }) {
  const { summary } = data
  const diff = summary.goalsFor - summary.goalsAgainst
  const rate = winRate(summary)
  return (
    <section aria-label="Bilans we wszystkich turniejach" className="space-y-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
        <Tile label="Turnieje" value={data.tournamentCount} />
        <Tile label="Mecze" value={summary.played} />
        <Tile label="Bilans Z-R-P" value={recordText(summary)} />
        <Tile
          label="Bramki"
          value={goalsText(summary)}
          hint={summary.played ? `różnica ${diff > 0 ? "+" : ""}${diff}` : undefined}
        />
        <Tile label="Zwycięstwa" value={rate == null ? "–" : `${rate}%`} />
      </div>
      {data.form.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 text-sm" aria-label="Forma">
          <span className="text-muted-foreground">Forma</span>
          {data.form.map((outcome, i) => (
            <OutcomeBadge key={i} outcome={outcome} />
          ))}
        </div>
      )}
    </section>
  )
}

function Tournaments({ data }: { data: TeamHistory }) {
  return (
    <section aria-label="Turnieje">
      <Heading>Turnieje</Heading>
      <ol className="divide-y rounded-lg border">
        {data.tournaments.map((t) => {
          const champion = t.resultKind === "Champion"
          const podium = t.resultKind === "RunnerUp" || t.resultKind === "Third"
          return (
            <li
              key={t.tournamentTeamId}
              className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2.5 text-sm"
              data-testid="history-tournament"
            >
              <span className="min-w-0 basis-full sm:flex-1 sm:basis-0">
                <Link
                  href={`/tournaments/${t.tournamentId}/teams/${t.tournamentTeamId}`}
                  className="block truncate font-medium hover:underline"
                >
                  {t.tournamentName}
                </Link>
                <span className="text-muted-foreground block truncate text-xs">
                  {[
                    t.categoryName,
                    t.startAt && t.endAt ? formatDateRange(t.startAt, t.endAt) : null,
                  ]
                    .filter(Boolean)
                    .join(" · ")}
                </span>
              </span>
              <span
                className={cn(
                  "flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium",
                  champion
                    ? "bg-primary text-primary-foreground"
                    : podium
                      ? "bg-primary/15 text-primary"
                      : "bg-muted",
                )}
              >
                {champion && <Trophy className="size-3.5" />}
                {tournamentResult(t)}
              </span>
              <span className="text-muted-foreground ml-auto shrink-0 text-right font-mono text-xs tabular-nums sm:w-24">
                {t.record.played
                  ? `${recordText(t.record)} · ${goalsText(t.record)}`
                  : "bez meczów"}
              </span>
            </li>
          )
        })}
      </ol>
    </section>
  )
}

function Players({ data }: { data: TeamHistory }) {
  return (
    <section aria-label="Zawodnicy">
      <Heading>Zawodnicy</Heading>
      {data.players.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Nikt jeszcze nie strzelił gola ani nie dostał kartki.
        </p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-muted-foreground text-xs">
            <tr>
              <th className="py-1 text-left font-medium">Zawodnik</th>
              <th className="w-10 py-1 text-center font-medium" title="Gole">
                G
              </th>
              <th className="w-10 py-1 text-center font-medium" title="Asysty">
                A
              </th>
              <th className="w-12 py-1 text-center font-medium" title="Nagrody MVP meczu">
                MVP
              </th>
              <th className="w-16 py-1 text-center font-medium" title="Żółte i czerwone kartki">
                Kartki
              </th>
            </tr>
          </thead>
          <tbody>
            {data.players.map((p) => (
              <tr key={p.playerId} className="border-t" data-testid="history-player">
                <td className="truncate py-1.5 font-medium">
                  {p.firstName} {p.lastName}
                </td>
                <td className="text-center font-mono tabular-nums">{p.goals}</td>
                <td className="text-center font-mono tabular-nums">{p.assists}</td>
                <td className="text-center font-mono tabular-nums">{p.mvp}</td>
                <td className="text-center font-mono text-xs tabular-nums">
                  {p.yellowCards || p.redCards ? (
                    <span className="inline-flex items-center gap-1.5">
                      <span className="inline-flex items-center gap-0.5">
                        <span
                          className="inline-block h-3 w-2 rounded-[2px] bg-yellow-400"
                          aria-hidden
                        />
                        {p.yellowCards}
                      </span>
                      <span className="inline-flex items-center gap-0.5">
                        <span
                          className="inline-block h-3 w-2 rounded-[2px] bg-red-500"
                          aria-hidden
                        />
                        {p.redCards}
                      </span>
                      <span className="sr-only">
                        {p.yellowCards} żółtych, {p.redCards} czerwonych
                      </span>
                    </span>
                  ) : (
                    "–"
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

function Opponents({ data }: { data: TeamHistory }) {
  return (
    <section aria-label="Rywale">
      <Heading>Rywale</Heading>
      {data.opponents.length === 0 ? (
        <p className="text-muted-foreground text-sm">Brak zakończonych meczów.</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-muted-foreground text-xs">
            <tr>
              <th className="py-1 text-left font-medium">Drużyna</th>
              <th className="w-10 py-1 text-center font-medium">M</th>
              <th className="w-16 py-1 text-center font-medium">Z-R-P</th>
              <th className="w-16 py-1 text-center font-medium">Bramki</th>
            </tr>
          </thead>
          <tbody>
            {data.opponents.map((o) => (
              <tr key={o.teamId} className="border-t" data-testid="history-opponent">
                <td className="truncate py-1.5 font-medium">{o.name}</td>
                <td className="text-center font-mono tabular-nums">{o.record.played}</td>
                <td className="text-center font-mono tabular-nums">{recordText(o.record)}</td>
                <td className="text-center font-mono tabular-nums">{goalsText(o.record)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
