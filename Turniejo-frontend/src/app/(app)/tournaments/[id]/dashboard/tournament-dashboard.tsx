"use client"

import {
  CalendarClock,
  CalendarDays,
  type LucideIcon,
  Layers,
  Settings,
  Shield,
  Trophy,
} from "lucide-react"
import Link from "next/link"

import { EmptyState } from "@/components/layout/empty-state"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { AnnouncementCard } from "@/features/notifications/components/announcement-card"
import { useTournamentDashboard } from "@/features/tournaments/api"
import { AccessCodesPanel } from "@/features/tournaments/components/access-codes-panel"
import { TournamentStatusStepper } from "@/features/tournaments/components/status-stepper"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { formatDateTime } from "@/lib/format"
import type { TournamentDashboard } from "@/types/tournaments"

const SCHEDULE_STATUS_LABELS: Record<TournamentDashboard["scheduleStatus"], string> = {
  NotGenerated: "Brak",
  Generated: "Gotowy",
  InProgress: "W trakcie",
  Completed: "Zakończony",
}

function StatCard({
  label,
  value,
  hint,
}: {
  label: string
  value: string | number
  hint?: string
}) {
  return (
    <Card className="gap-1 py-4">
      <CardContent className="px-4">
        <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{label}</p>
        {/* Liczby w mono (stała szerokość przy odświeżaniu), tekst zwykłym krojem — inaczej się nie mieści. */}
        <p
          className={
            typeof value === "number" || /^[\d/]+$/.test(value)
              ? "mt-1 font-mono text-2xl font-semibold tabular-nums"
              : "mt-1 text-base leading-tight font-semibold"
          }
        >
          {value}
        </p>
        {hint && <p className="text-muted-foreground mt-1 text-xs">{hint}</p>}
      </CardContent>
    </Card>
  )
}

function StatCards({ dashboard }: { dashboard: TournamentDashboard }) {
  const { teams, matches } = dashboard
  return (
    <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
      <StatCard label="Drużyny" value={teams.total} hint={`${teams.accepted} zaakceptowanych`} />
      <StatCard label="Oczekujące" value={teams.pending} hint="zgłoszenia do decyzji" />
      <StatCard
        label="Mecze"
        value={`${matches.finished}/${matches.total}`}
        hint={`${matches.scheduled} zaplanowanych`}
      />
      <StatCard label="Harmonogram" value={SCHEDULE_STATUS_LABELS[dashboard.scheduleStatus]} />
    </div>
  )
}

function UpcomingMatches({ dashboard }: { dashboard: TournamentDashboard }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Najbliższe mecze</CardTitle>
      </CardHeader>
      <CardContent>
        {dashboard.upcomingMatches.length === 0 ? (
          <EmptyState
            icon={CalendarClock}
            title="Brak zaplanowanych meczów"
            description="Mecze pojawią się po wygenerowaniu harmonogramu."
          />
        ) : (
          <ul className="divide-y">
            {dashboard.upcomingMatches.map((match) => (
              <li
                key={match.matchId}
                className="flex items-center justify-between gap-3 py-2 text-sm"
              >
                <span className="min-w-0 truncate">
                  {match.homeTeamName} <span className="text-muted-foreground">vs</span>{" "}
                  {match.awayTeamName}
                </span>
                <span className="text-muted-foreground shrink-0 font-mono tabular-nums">
                  {formatDateTime(match.scheduledAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

type QuickAction = { href: string; label: string; icon: LucideIcon; hint: string }

function QuickActions({ id }: { id: string }) {
  const actions: QuickAction[] = [
    {
      href: `/tournaments/${id}/categories`,
      label: "Kategorie i grupy",
      icon: Layers,
      hint: "Podział wiekowy i grupy",
    },
    {
      href: `/tournaments/${id}/settings`,
      label: "Ustawienia",
      icon: Settings,
      hint: "Format, punktacja, czas meczu",
    },
    {
      href: `/tournaments/${id}/teams`,
      label: "Drużyny",
      icon: Shield,
      hint: "Zgłoszenia i składy",
    },
    {
      href: `/tournaments/${id}/schedule`,
      label: "Harmonogram",
      icon: CalendarDays,
      hint: "Boiska i terminarz",
    },
  ]
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Szybkie akcje</CardTitle>
      </CardHeader>
      <CardContent className="grid gap-2 sm:grid-cols-2">
        {actions.map(({ href, label, icon: Icon, hint }) => (
          <Link
            key={href}
            href={href}
            className="hover:border-primary/50 hover:bg-accent focus-visible:ring-ring flex items-start gap-3 rounded-lg border p-3 transition-colors outline-none focus-visible:ring-2"
          >
            <Icon className="text-primary mt-0.5 size-4 shrink-0" />
            <span>
              <span className="block text-sm font-medium">{label}</span>
              <span className="text-muted-foreground block text-xs">{hint}</span>
            </span>
          </Link>
        ))}
      </CardContent>
    </Card>
  )
}

export function TournamentDashboardView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const dashboard = useTournamentDashboard(id)

  return (
    <div className="space-y-6">
      {dashboard.isPending ? (
        <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24 rounded-xl" />
          ))}
        </div>
      ) : dashboard.data ? (
        <StatCards dashboard={dashboard.data} />
      ) : (
        <EmptyState icon={Trophy} title="Nie udało się pobrać podsumowania" />
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

      <div className="grid gap-6 xl:grid-cols-2">
        {dashboard.data && <UpcomingMatches dashboard={dashboard.data} />}
        {isOrganizer && <QuickActions id={id} />}
      </div>

      {isOrganizer && <AnnouncementCard tournamentId={id} />}

      {isOrganizer && tournament.data && <AccessCodesPanel tournament={tournament.data} />}
    </div>
  )
}
