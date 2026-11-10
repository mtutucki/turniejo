import { Trophy } from "lucide-react"
import Link from "next/link"
import type { ReactNode } from "react"

import { resultNote } from "@/features/matches/result-note"
import { formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match } from "@/types/matches"

import {
  type CupPath,
  type Outcome,
  OUTCOME_LABELS,
  OUTCOME_LETTERS,
  outcomeFor,
  type TeamRecord,
} from "../team-record"

// Części strony drużyny wspólne dla portalu kibica i panelu: bilans, forma, droga w pucharze,
// lista meczów. Dane przychodzą z zewnątrz (każde miejsce pobiera je swoimi hookami).

export type TeamLinks = {
  teamName: (tournamentTeamId: string) => string
  /** Strona innej drużyny (przeciwnika). Bez niej nazwa jest zwykłym tekstem. */
  teamHref?: (tournamentTeamId: string) => string | undefined
  /** Szczegóły meczu. Bez niej wiersz nie jest linkiem. */
  matchHref?: (match: Match) => string | undefined
}

const OUTCOME_STYLES: Record<Outcome, string> = {
  W: "bg-primary text-primary-foreground",
  D: "bg-muted text-foreground border",
  L: "bg-destructive/15 text-destructive",
}

export function OutcomeBadge({ outcome, size = "sm" }: { outcome: Outcome; size?: "sm" | "md" }) {
  return (
    <span
      title={OUTCOME_LABELS[outcome]}
      aria-label={OUTCOME_LABELS[outcome]}
      className={cn(
        "inline-flex shrink-0 items-center justify-center rounded-md font-semibold",
        size === "md" ? "size-7 text-sm" : "size-6 text-xs",
        OUTCOME_STYLES[outcome],
      )}
    >
      {OUTCOME_LETTERS[outcome]}
    </span>
  )
}

function Tile({ label, value, hint }: { label: string; value: ReactNode; hint?: string }) {
  return (
    <div className="bg-card min-w-0 rounded-xl border p-3">
      <p className="text-muted-foreground text-xs">{label}</p>
      <p className="mt-1 truncate font-mono text-xl font-semibold tabular-nums">{value}</p>
      {hint && <p className="text-muted-foreground mt-0.5 truncate text-xs">{hint}</p>}
    </div>
  )
}

export type TeamStanding = { position: number; points: number; groupName: string }

/** Kafelki: miejsce w grupie, mecze, bilans, bramki, punkty i forma z ostatnich meczów. */
export function TeamStats({
  record,
  standing,
}: {
  record: TeamRecord<Match>
  standing?: TeamStanding | null
}) {
  const diff = record.goalsFor - record.goalsAgainst
  return (
    <section aria-label="Bilans drużyny" className="space-y-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
        {standing && (
          <Tile label="Miejsce" value={`${standing.position}.`} hint={standing.groupName} />
        )}
        <Tile label="Mecze" value={record.played} />
        <Tile label="Bilans Z-R-P" value={`${record.wins}-${record.draws}-${record.losses}`} />
        <Tile
          label="Bramki"
          value={`${record.goalsFor}:${record.goalsAgainst}`}
          hint={record.played ? `różnica ${diff > 0 ? "+" : ""}${diff}` : undefined}
        />
        {standing && <Tile label="Punkty" value={standing.points} hint="w tabeli grupy" />}
      </div>
      {record.form.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 text-sm" aria-label="Forma">
          <span className="text-muted-foreground">Forma</span>
          {record.form.map((outcome, i) => (
            <OutcomeBadge key={i} outcome={outcome} />
          ))}
        </div>
      )}
    </section>
  )
}

/** Droga w pucharze: rundy po kolei, przeciwnik, wynik, a na górze krótki status. */
export function CupPathCard({
  cup,
  teamId,
  links,
  showScores = true,
}: {
  cup: CupPath
  teamId: string
  links: TeamLinks
  /** Wynik trwającego meczu ukryty, gdy portal nie pokazuje meczów na żywo. */
  showScores?: boolean
}) {
  return (
    <section aria-label="Puchar" className="bg-card rounded-xl border">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-3">
        <h2 className="font-semibold">Puchar</h2>
        <span
          className={cn(
            "flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium",
            cup.champion ? "bg-primary text-primary-foreground" : "bg-muted",
          )}
        >
          {cup.champion && <Trophy className="size-3.5" />}
          {cup.status}
        </span>
      </header>
      <ol className="divide-y">
        {cup.steps.map((step) => {
          if (step.kind === "planned") {
            const { planned } = step
            const opponent = planned.home.tournamentTeamId === teamId ? planned.away : planned.home
            return (
              <li key={planned.nodeId} className="flex items-center gap-3 px-4 py-3 text-sm">
                <span className="text-muted-foreground w-28 shrink-0 text-xs">{step.stage}</span>
                <span className="min-w-0 flex-1 truncate">
                  <span className="text-muted-foreground">vs </span>
                  {opponent.tournamentTeamId ? (
                    <TeamName id={opponent.tournamentTeamId} links={links} />
                  ) : (
                    <span className="text-muted-foreground italic">{opponent.label}</span>
                  )}
                </span>
                {planned.scheduledAt && (
                  <span className="text-muted-foreground shrink-0 text-xs">
                    {formatDateTime(planned.scheduledAt)}
                  </span>
                )}
              </li>
            )
          }
          const m = step.match as Match
          return (
            <li key={m.id} className="flex items-center gap-3 px-4 py-3 text-sm">
              <span className="text-muted-foreground w-28 shrink-0 text-xs">{step.stage}</span>
              <span className="min-w-0 flex-1 truncate">
                <span className="text-muted-foreground">vs </span>
                <TeamName id={opponentOf(m, teamId)} links={links} />
              </span>
              <Score match={m} teamId={teamId} links={links} showScore={showScores} />
            </li>
          )
        })}
      </ol>
    </section>
  )
}

const opponentOf = (m: Match, teamId: string) =>
  m.homeTournamentTeamId === teamId ? m.awayTournamentTeamId : m.homeTournamentTeamId

function TeamName({ id, links }: { id: string; links: TeamLinks }) {
  const href = links.teamHref?.(id)
  const name = links.teamName(id)
  return href ? (
    <Link href={href} className="font-medium hover:underline">
      {name}
    </Link>
  ) : (
    <span className="font-medium">{name}</span>
  )
}

/** Wynik z perspektywy drużyny („2:1” = jej bramki pierwsze) z literą Z/R/P albo godzina meczu. */
function Score({
  match,
  teamId,
  links,
  showScore,
}: {
  match: Match
  teamId: string
  links: TeamLinks
  showScore: boolean
}) {
  const live = match.status === "Live" || match.status === "Paused"
  const outcome = outcomeFor(match, teamId)
  const home = match.homeTournamentTeamId === teamId
  const scored = (live && showScore) || match.status === "Finished"
  const note = resultNote(match)
  const content = scored ? (
    <span className="flex items-center gap-2">
      <span className={cn("font-mono font-semibold tabular-nums", live && "text-live")}>
        {home ? match.homeScore : match.awayScore}:{home ? match.awayScore : match.homeScore}
      </span>
      {note && <span className="text-muted-foreground hidden text-xs sm:inline">{note}</span>}
      {outcome && <OutcomeBadge outcome={outcome} />}
      {live && <span className="text-live text-xs font-semibold">LIVE</span>}
    </span>
  ) : match.status === "Cancelled" ? (
    <span className="text-muted-foreground text-xs">Odwołany</span>
  ) : (
    <span className="text-muted-foreground text-xs">{formatDateTime(match.scheduledAt)}</span>
  )
  const href = links.matchHref?.(match)
  return href ? (
    <Link
      href={href}
      aria-label={`Mecz z ${links.teamName(opponentOf(match, teamId))}`}
      className="hover:bg-accent -mx-1 shrink-0 rounded-md px-1 py-0.5"
    >
      {content}
    </Link>
  ) : (
    <span className="shrink-0">{content}</span>
  )
}

/** Wszystkie mecze drużyny po kolei: termin, przeciwnik (dom/wyjazd), etap i wynik. */
export function TeamMatchList({
  record,
  teamId,
  links,
  stageLabel,
}: {
  record: TeamRecord<Match>
  teamId: string
  links: TeamLinks
  /** „Grupa A”, „Półfinał”… */
  stageLabel: (match: Match) => string | null
}) {
  return (
    <ol className="divide-y">
      {record.matches.map((m) => {
        const home = m.homeTournamentTeamId === teamId
        const stage = stageLabel(m)
        return (
          <li key={m.id} className="flex items-center gap-3 py-3 text-sm" data-testid="team-match">
            <span className="text-muted-foreground w-24 shrink-0 text-xs">
              {formatDateTime(m.scheduledAt)}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate">
                <span className="text-muted-foreground">vs </span>
                <TeamName id={opponentOf(m, teamId)} links={links} />
              </span>
              <span className="text-muted-foreground block truncate text-xs">
                {[stage, home ? "gospodarz" : "gość"].filter(Boolean).join(" · ")}
              </span>
            </span>
            <Score match={m} teamId={teamId} links={links} showScore />
          </li>
        )
      })}
    </ol>
  )
}
