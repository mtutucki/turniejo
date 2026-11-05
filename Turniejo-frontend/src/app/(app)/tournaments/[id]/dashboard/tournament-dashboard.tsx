"use client"

import { ChevronRight, ExternalLink, EyeOff, Globe, Trophy } from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { AnnouncementCard } from "@/features/notifications/components/announcement-card"
import { useTournamentDashboard } from "@/features/tournaments/api"
import { AccessCodesPanel } from "@/features/tournaments/components/access-codes-panel"
import { SetupProgressCard } from "@/features/tournaments/components/setup-progress-card"
import { TournamentStatusStepper } from "@/features/tournaments/components/status-stepper"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { cn } from "@/lib/utils"
import { type Tournament, type TournamentDashboard, statusAtLeast } from "@/types/tournaments"

const SCHEDULE_STATUS_LABELS: Record<TournamentDashboard["scheduleStatus"], string> = {
  NotGenerated: "terminarz jeszcze nie powstał",
  Generated: "terminarz gotowy",
  InProgress: "turniej trwa",
  Completed: "wszystkie rozegrane",
}

function StatCard({
  label,
  value,
  hint,
  href,
  highlight = false,
}: {
  label: string
  value: string | number
  hint: string
  /** Kafelek prowadzi do miejsca, w którym tę liczbę się zmienia. */
  href: string
  /** Coś czeka na decyzję organizatora. */
  highlight?: boolean
}) {
  return (
    <Link
      href={href}
      className="focus-visible:ring-ring group block rounded-xl outline-none focus-visible:ring-2"
    >
      <Card
        className={cn(
          "group-hover:border-primary/50 h-full gap-1 py-4 transition-colors",
          highlight && "border-primary/40 bg-primary/5",
        )}
      >
        <CardContent className="px-4">
          <p className="text-muted-foreground flex items-center justify-between text-sm">
            {label}
            <ChevronRight className="size-4 opacity-0 transition-opacity group-hover:opacity-100" />
          </p>
          <p className="mt-1 text-3xl leading-none font-semibold tabular-nums">{value}</p>
          <p className="text-muted-foreground mt-2 text-xs">{hint}</p>
        </CardContent>
      </Card>
    </Link>
  )
}

function StatCards({
  id,
  dashboard,
  teamLimit,
  isOrganizer,
}: {
  id: string
  dashboard: TournamentDashboard
  teamLimit: number | undefined
  isOrganizer: boolean
}) {
  const { teams, matches } = dashboard
  return (
    <div className={cn("grid gap-3", isOrganizer ? "sm:grid-cols-3" : "sm:grid-cols-2")}>
      {/* Liczymy drużyny, które grają: teams.total obejmuje też zgłoszenia odrzucone i wycofane. */}
      <StatCard
        label="Drużyny"
        value={teamLimit ? `${teams.accepted}/${teamLimit}` : teams.accepted}
        hint={teamLimit ? "zaakceptowane / limit" : "zaakceptowane"}
        href={`/tournaments/${id}/teams`}
      />
      {/* O zgłoszeniach decyduje tylko organizator — pozostałym rolom ta liczba nic nie mówi. */}
      {isOrganizer && (
        <StatCard
          label="Zgłoszenia"
          value={teams.pending}
          hint={teams.pending > 0 ? "czekają na Twoją decyzję" : "nic nie czeka na decyzję"}
          href={`/tournaments/${id}/teams`}
          highlight={teams.pending > 0}
        />
      )}
      <StatCard
        label="Mecze"
        value={matches.total > 0 ? `${matches.finished}/${matches.total}` : "–"}
        hint={
          matches.total > 0 && dashboard.scheduleStatus === "Generated"
            ? "rozegrane / wszystkie"
            : SCHEDULE_STATUS_LABELS[dashboard.scheduleStatus]
        }
        href={`/tournaments/${id}/schedule`}
      />
    </div>
  )
}

function PublicPortalCard({
  tournament,
  canManage,
}: {
  tournament: Tournament
  canManage: boolean
}) {
  const path = `/t/${tournament.slug}`
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Portal publiczny</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        {tournament.isPublic ? (
          <>
            <p className="flex items-start gap-2">
              <Globe className="text-primary mt-0.5 size-4 shrink-0" aria-hidden />
              Turniej jest publiczny - kibice widzą go bez logowania.
            </p>
            <Link
              href={path}
              target="_blank"
              rel="noopener noreferrer"
              className="text-primary inline-flex items-center gap-1.5 font-medium underline-offset-4 hover:underline"
            >
              <code className="font-mono">{path}</code>
              <ExternalLink className="size-3.5" aria-hidden />
            </Link>
          </>
        ) : (
          <>
            <p className="text-muted-foreground flex items-start gap-2">
              <EyeOff className="mt-0.5 size-4 shrink-0" aria-hidden />
              Turniej nie jest publiczny - portal kibica jest wyłączony.
            </p>
            {canManage && (
              <Link
                href={`/tournaments/${tournament.id}/settings#portal`}
                className="text-primary font-medium underline-offset-4 hover:underline"
              >
                Włącz w ustawieniach
              </Link>
            )}
          </>
        )}
      </CardContent>
    </Card>
  )
}

export function TournamentDashboardView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const dashboard = useTournamentDashboard(id)

  // Dwie kolumny o ograniczonej szerokości: po lewej to, co się dzieje w turnieju, po prawej rzeczy
  // "do ręki" (kody, portal). Na całą szerokość dużego monitora karty były w większości puste.
  return (
    <div className="grid max-w-7xl gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
      <div className="space-y-6">
        {/* Dopóki nie ma terminarza, najważniejsze na tej stronie jest to, czego jeszcze brakuje. */}
        {isOrganizer &&
          tournament.data &&
          !statusAtLeast(tournament.data.status, "ScheduleGenerated") && (
            <SetupProgressCard tournament={tournament.data} />
          )}
        {tournament.data && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Etap turnieju</CardTitle>
            </CardHeader>
            <CardContent>
              <TournamentStatusStepper tournament={tournament.data} canManage={isOrganizer} />
            </CardContent>
          </Card>
        )}

        {dashboard.isPending ? (
          <div className="grid gap-3 sm:grid-cols-3">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-28 rounded-xl" />
            ))}
          </div>
        ) : dashboard.data ? (
          <StatCards
            id={id}
            dashboard={dashboard.data}
            teamLimit={tournament.data?.teamLimit}
            isOrganizer={isOrganizer}
          />
        ) : (
          <EmptyState icon={Trophy} title="Nie udało się pobrać podsumowania" />
        )}

        {isOrganizer && <AnnouncementCard tournamentId={id} />}
      </div>

      <div className="space-y-6">
        {isOrganizer && tournament.data && <AccessCodesPanel tournament={tournament.data} />}
        {tournament.data && (
          <PublicPortalCard tournament={tournament.data} canManage={isOrganizer} />
        )}
      </div>
    </div>
  )
}
