"use client"

import {
  CalendarCheck,
  CalendarClock,
  Clock,
  Eraser,
  Hourglass,
  Lock,
  MapPin,
  Radio,
  UserRound,
  Whistle,
} from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useEffect, useMemo, useState } from "react"
import { toast } from "sonner"

import { EmptyState } from "@/components/layout/empty-state"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { useBrackets, useClearSchedule, usePitches, useSchedule } from "@/features/matches/api"
import {
  AssignRefereeDialog,
  memberName,
} from "@/features/matches/components/assign-referee-dialog"
import { MatchStatusBadge } from "@/features/matches/components/match-status-badge"
import {
  type PlannedBracketMatch,
  plannedBracketMatches,
} from "@/features/matches/planned-bracket-matches"
import { RescheduleMatchDialog } from "@/features/matches/components/reschedule-match-dialog"
import { useTournamentMembers, useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { formatDayLong, formatTime, plural, toZonedDateInput } from "@/lib/format"
import { cn } from "@/lib/utils"
import { type Match, type Pitch, SCHEDULABLE_STATUSES } from "@/types/matches"
import type { TournamentMember } from "@/types/teams"
import { UserRole } from "@/types/roles"

import { hasScheduleDraft } from "./generate/generate-schedule"

const ALL = "all"

type Lookups = {
  teamName: (tournamentTeamId: string) => string
  pitchName: (pitchId: string | null) => string | null
  groupLabel: (match: Match) => string | null
  referee: (userId: string | null) => TournamentMember | undefined
}

function FilterSelect({
  label,
  value,
  onChange,
  options,
  allLabel,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: { id: string; name: string }[]
  allLabel: string
}) {
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger size="sm" className="w-full sm:w-44" aria-label={label}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL}>{allLabel}</SelectItem>
        {options.map((o) => (
          <SelectItem key={o.id} value={o.id}>
            {o.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function MatchRow({
  tournamentId,
  match,
  lookups,
  isOrganizer,
  pitches,
  referees,
  refereeCode,
  unscheduled = false,
}: {
  tournamentId: string
  match: Match
  lookups: Lookups
  isOrganizer: boolean
  pitches: Pitch[]
  referees: TournamentMember[]
  refereeCode: string | null
  /** Para z drabinki przed wygenerowaniem terminarza — godzina i boisko w bazie są tylko zaślepką. */
  unscheduled?: boolean
}) {
  const home = lookups.teamName(match.homeTournamentTeamId)
  const away = lookups.teamName(match.awayTournamentTeamId)
  const title = `${home} – ${away}`
  const pitch = lookups.pitchName(match.pitchId)
  const group = lookups.groupLabel(match)
  const referee = lookups.referee(match.refereeId)
  const editable = SCHEDULABLE_STATUSES.includes(match.status)
  const showScore =
    match.status === "Live" || match.status === "Paused" || match.status === "Finished"

  return (
    <li className="flex flex-col gap-3 py-3 lg:flex-row lg:items-center">
      <div className="flex min-w-0 flex-1 items-start gap-4">
        <span className="w-12 shrink-0 pt-0.5 font-mono text-sm font-semibold tabular-nums">
          {unscheduled ? "–:–" : formatTime(match.scheduledAt)}
        </span>
        <div className="min-w-0 flex-1 space-y-1">
          <p className="flex flex-wrap items-center gap-x-2 font-medium">
            <span className="truncate">{home}</span>
            {showScore ? (
              <span className="font-mono tabular-nums">
                {match.homeScore}:{match.awayScore}
              </span>
            ) : (
              <span className="text-muted-foreground text-sm">vs</span>
            )}
            <span className="truncate">{away}</span>
          </p>
          <p className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
            <MatchStatusBadge status={match.status} />
            <span className="flex items-center gap-1">
              <MapPin className="size-3" />
              {unscheduled ? "Bez terminu" : (pitch ?? "Bez boiska")}
            </span>
            {group && <span>{group}</span>}
            {isOrganizer && (
              <span className={cn("flex items-center gap-1", !referee && "text-foreground")}>
                <UserRound className="size-3" />
                {referee ? memberName(referee) : "Brak sędziego"}
              </span>
            )}
          </p>
        </div>
      </div>

      {isOrganizer && !unscheduled && (
        <div className="flex flex-wrap items-center gap-2 pl-16 lg:pl-0">
          {editable ? (
            <div className="flex gap-2">
              <RescheduleMatchDialog
                tournamentId={tournamentId}
                match={match}
                title={title}
                pitches={pitches}
                trigger={
                  <Button size="sm" variant="outline" aria-label={`Zmień termin: ${title}`}>
                    <Clock />
                    Termin
                  </Button>
                }
              />
              <AssignRefereeDialog
                tournamentId={tournamentId}
                match={match}
                title={title}
                referees={referees}
                refereeCode={refereeCode}
                trigger={
                  <Button size="sm" variant="outline" aria-label={`Sędzia: ${title}`}>
                    <Whistle />
                    Sędzia
                  </Button>
                }
              />
            </div>
          ) : (
            <Tooltip>
              <TooltipTrigger asChild>
                <span
                  tabIndex={0}
                  className="text-muted-foreground flex items-center gap-1 text-xs"
                >
                  <Lock className="size-3.5" />
                  Zablokowany
                </span>
              </TooltipTrigger>
              <TooltipContent>
                Mecz się rozpoczął, zakończył albo został odwołany — termin, boiska i sędziego nie
                można już zmienić.
              </TooltipContent>
            </Tooltip>
          )}
          {/* Organizator może poprowadzić mecz zamiast sędziego (te same uprawnienia w API). */}
          {match.status !== "Cancelled" && (
            <Button asChild size="sm" variant="ghost" aria-label={`Panel meczu: ${title}`}>
              <Link href={`/matches/${match.id}/live`}>
                <Radio />
                Panel
              </Link>
            </Button>
          )}
        </div>
      )}
    </li>
  )
}

/** Zapowiedź meczu kolejnej rundy: termin już jest, pary jeszcze nie (czeka na wcześniejsze mecze). */
function PlannedRow({ planned, pitch }: { planned: PlannedBracketMatch; pitch: string | null }) {
  const sideClass = (known: boolean) => cn("truncate", !known && "text-muted-foreground italic")
  return (
    <li className="flex items-start gap-4 py-3" data-testid="planned-bracket-match">
      <span className="text-muted-foreground w-12 shrink-0 pt-0.5 font-mono text-sm font-semibold tabular-nums">
        {formatTime(planned.scheduledAt)}
      </span>
      <div className="min-w-0 flex-1 space-y-1">
        <p className="flex flex-wrap items-center gap-x-2 font-medium">
          <span className={sideClass(planned.home.known)}>{planned.home.label}</span>
          <span className="text-muted-foreground text-sm">vs</span>
          <span className={sideClass(planned.away.known)}>{planned.away.label}</span>
        </p>
        <p className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
          <span className="flex items-center gap-1 rounded-full border border-dashed px-2 py-0.5">
            <Hourglass className="size-3" />
            Czeka na wynik wcześniejszych meczów
          </span>
          <span className="flex items-center gap-1">
            <MapPin className="size-3" />
            {pitch ?? "Bez boiska"}
          </span>
          <span>{planned.stage}</span>
        </p>
      </div>
    </li>
  )
}

function ClearScheduleButton({
  tournamentId,
  matchCount,
  knockout,
}: {
  tournamentId: string
  matchCount: number
  /** Puchar: mecze należą do drabinki, więc zostają — znikają tylko terminy. */
  knockout: boolean
}) {
  const clear = useClearSchedule(tournamentId)
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="outline" size="sm" disabled={clear.isPending}>
          <Eraser />
          Wyczyść terminarz
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Wyczyścić terminarz?</AlertDialogTitle>
          <AlertDialogDescription>
            {knockout
              ? "Pary z drabinki zostaną, ale stracą godziny i boiska, a turniej wróci do etapu „Zapisy zamknięte”. Terminy nadasz ponownie, generując terminarz."
              : `Usuniesz ${plural(matchCount, ["mecz", "mecze", "meczów"])} razem z przypisaniami sędziów, a turniej wróci do etapu „Zapisy zamknięte”. Potem możesz poprawić drużyny i grupy i wygenerować terminarz od nowa.`}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Anuluj</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            onClick={() =>
              clear.mutate(undefined, {
                onSuccess: ({ matchesRemoved }) =>
                  toast.success(
                    knockout
                      ? "Terminy wyczyszczone — pary czekają na nowy terminarz"
                      : `Usunięto ${plural(matchesRemoved, ["mecz", "mecze", "meczów"])}`,
                  ),
              })
            }
          >
            Wyczyść
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function TournamentScheduleView() {
  const { id, tournament, isOrganizer } = useTournamentContext()
  const router = useRouter()
  const schedule = useSchedule(id)
  const pitches = usePitches(id)
  const teams = useTournamentTeams(id)
  const categories = useCategories(id)
  const groups = useGroups(id)
  const members = useTournamentMembers(id, isOrganizer)
  const hasBracket =
    tournament.data?.format === "Knockout" || tournament.data?.format === "GroupAndKnockout"
  const bracketCategoryIds = (categories.data ?? []).map((c) => c.id)
  const brackets = useBrackets(id, bracketCategoryIds, hasBracket)

  const [categoryId, setCategoryId] = useState(ALL)
  const [groupId, setGroupId] = useState(ALL)
  const [pitchId, setPitchId] = useState(ALL)

  const lookups = useMemo<Lookups>(() => {
    const teamById = new Map((teams.data ?? []).map((t) => [t.id, t.teamName]))
    const pitchById = new Map((pitches.data ?? []).map((p) => [p.id, p.name]))
    const groupById = new Map((groups.data ?? []).map((g) => [g.id, g.name]))
    const categoryById = new Map((categories.data ?? []).map((c) => [c.id, c.name]))
    const memberById = new Map((members.data ?? []).map((m) => [m.userId, m]))
    return {
      teamName: (tid) => teamById.get(tid) ?? "Drużyna",
      pitchName: (pid) => (pid ? (pitchById.get(pid) ?? "Boisko") : null),
      groupLabel: (m) =>
        [
          m.categoryId && categoryById.get(m.categoryId),
          m.groupId ? groupById.get(m.groupId) : m.stageType !== "Group" && "Faza pucharowa",
        ]
          .filter(Boolean)
          .join(" · ") || null,
      referee: (uid) => (uid ? memberById.get(uid) : undefined),
    }
  }, [teams.data, pitches.data, groups.data, categories.data, members.data])

  const referees = (members.data ?? []).filter((m) => m.roles.includes(UserRole.Referee))
  const groupOptions = (groups.data ?? []).filter(
    (g) => categoryId === ALL || g.categoryId === categoryId,
  )

  const filtered = (schedule.data ?? [])
    .filter(
      (m) =>
        (categoryId === ALL || m.categoryId === categoryId) &&
        (groupId === ALL || m.groupId === groupId) &&
        (pitchId === ALL || m.pitchId === pitchId),
    )
    // Backend sortuje tylko po godzinie — mecze z tego samego slotu układamy po boisku,
    // inaczej wiersze zmieniają kolejność przy każdym odświeżeniu.
    .sort(
      (a, b) =>
        a.scheduledAt.localeCompare(b.scheduledAt) ||
        (lookups.pitchName(a.pitchId) ?? "").localeCompare(
          lookups.pitchName(b.pitchId) ?? "",
          "pl",
          {
            numeric: true,
          },
        ),
    )
  const anyStarted = (schedule.data ?? []).some(
    (m) => !SCHEDULABLE_STATUSES.includes(m.status) && m.status !== "Cancelled",
  )

  // Przed zamknięciem zapisów generator pokaże listę brakujących warunków.
  const status = tournament.data?.status
  const canGenerate =
    isOrganizer &&
    (status === "Draft" || status === "RegistrationOpen" || status === "RegistrationClosed")
  // Puchar: mecze 1. rundy istnieją już po utworzeniu drabinki, ale bez godzin — generator je rozstawia.
  const knockout = tournament.data?.format === "Knockout"
  // Pary z drabinki przed (ponownym) wygenerowaniem terminarza: godziny i boiska w bazie to zaślepki
  // albo pozostałość po wyczyszczonym terminarzu — nie pokazujemy ich jak obowiązujących terminów.
  const awaitingSchedule =
    knockout &&
    (status === "Draft" || status === "RegistrationOpen" || status === "RegistrationClosed")

  // Mecze kolejnych rund pucharu, które jeszcze nie istnieją, ale mają zaplanowany termin.
  const planned = awaitingSchedule
    ? []
    : brackets
        .flatMap((bracket, i) =>
          bracket.data
            ? plannedBracketMatches(bracket.data, bracketCategoryIds[i]!, lookups.teamName)
            : [],
        )
        .filter(
          (p) =>
            (categoryId === ALL || p.categoryId === categoryId) &&
            groupId === ALL &&
            (pitchId === ALL || p.pitchId === pitchId),
        )

  type Row =
    | { kind: "match"; at: string; match: Match }
    | { kind: "planned"; at: string; planned: PlannedBracketMatch }
  const rows: Row[] = [
    ...filtered.map((match): Row => ({ kind: "match", at: match.scheduledAt, match })),
    ...planned.map((p): Row => ({ kind: "planned", at: p.scheduledAt, planned: p })),
  ]
  // Stabilne sortowanie: mecze zachowują kolejność z `filtered`, zapowiedzi wchodzą między nie po godzinie.
  if (!awaitingSchedule) rows.sort((a, b) => a.at.localeCompare(b.at))

  // Bez Map.groupBy — brak go w Safari < 17.4.
  const days = new Map<string, Row[]>()
  for (const row of rows) {
    const key = awaitingSchedule ? "unscheduled" : toZonedDateInput(row.at)
    days.set(key, [...(days.get(key) ?? []), row])
  }

  // Organizator jest w trakcie generowania (otworzył formularz i wyszedł np. dodać drużyny) —
  // wraca prosto do formularza. "Anuluj" i "← Terminarz" kończą ten tryb.
  const resumeGenerator = canGenerate && hasScheduleDraft(id)
  useEffect(() => {
    if (resumeGenerator) router.replace(`/tournaments/${id}/schedule/generate`)
  }, [resumeGenerator, router, id])

  if (schedule.isPending || teams.isPending || resumeGenerator) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-9 w-full max-w-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }

  if (!schedule.data?.length) {
    return (
      <EmptyState
        icon={CalendarClock}
        title="Brak terminarza"
        description={
          isOrganizer
            ? knockout
              ? "Puchar: najpierw utwórz drabinkę (pary 1. rundy), potem wygeneruj terminarz."
              : "Wygeneruj terminarz, gdy zapisy są zamknięte, a drużyny przypisane do grup."
            : "Organizator nie wygenerował jeszcze terminarza."
        }
        action={
          canGenerate ? (
            <Button asChild>
              <Link href={`/tournaments/${id}/${knockout ? "bracket" : "schedule/generate"}`}>
                <CalendarCheck />
                {knockout ? "Utwórz drabinkę" : "Generuj terminarz"}
              </Link>
            </Button>
          ) : undefined
        }
      />
    )
  }

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">
            {awaitingSchedule ? "Pary z drabinki" : "Terminarz"}
          </h2>
          <p className="text-muted-foreground text-sm">
            {plural(filtered.length, ["mecz", "mecze", "meczów"])}
            {filtered.length !== schedule.data.length && ` z ${schedule.data.length}`}
            {awaitingSchedule &&
              " · bez terminów — wygeneruj terminarz, żeby nadać godziny i boiska"}
          </p>
        </div>
        {isOrganizer && (
          <div className="flex flex-wrap gap-2">
            {knockout && canGenerate && (
              <Button asChild size="sm">
                <Link href={`/tournaments/${id}/schedule/generate`}>
                  <CalendarCheck />
                  Generuj terminarz
                </Link>
              </Button>
            )}
            {status === "ScheduleGenerated" && !anyStarted && (
              <ClearScheduleButton
                tournamentId={id}
                matchCount={schedule.data.length}
                knockout={knockout}
              />
            )}
            <Button asChild variant="outline" size="sm">
              <Link href={`/tournaments/${id}/pitches`}>
                <MapPin />
                Boiska
              </Link>
            </Button>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 gap-2 sm:flex sm:flex-wrap">
        <FilterSelect
          label="Kategoria"
          value={categoryId}
          onChange={(value) => {
            setCategoryId(value)
            setGroupId(ALL)
          }}
          options={categories.data ?? []}
          allLabel="Wszystkie kategorie"
        />
        <FilterSelect
          label="Grupa"
          value={groupId}
          onChange={setGroupId}
          options={groupOptions}
          allLabel="Wszystkie grupy"
        />
        <FilterSelect
          label="Boisko"
          value={pitchId}
          onChange={setPitchId}
          options={pitches.data ?? []}
          allLabel="Wszystkie boiska"
        />
      </div>

      {rows.length === 0 ? (
        <EmptyState icon={CalendarClock} title="Brak meczów dla wybranych filtrów" />
      ) : (
        [...days.entries()].map(([day, dayRows]) => (
          <Card key={day} className="gap-0 py-0">
            <CardContent className="px-4">
              <h3 className="text-muted-foreground border-b py-3 text-sm font-medium first-letter:uppercase">
                {awaitingSchedule ? "Bez terminu" : formatDayLong(dayRows[0]!.at)}
              </h3>
              <ul className="divide-y">
                {dayRows.map((row) =>
                  row.kind === "planned" ? (
                    <PlannedRow
                      key={row.planned.nodeId}
                      planned={row.planned}
                      pitch={lookups.pitchName(row.planned.pitchId)}
                    />
                  ) : (
                    <MatchRow
                      key={row.match.id}
                      tournamentId={id}
                      match={row.match}
                      lookups={lookups}
                      isOrganizer={isOrganizer}
                      pitches={pitches.data ?? []}
                      referees={referees}
                      refereeCode={tournament.data?.refereeCode ?? null}
                      unscheduled={awaitingSchedule}
                    />
                  ),
                )}
              </ul>
            </CardContent>
          </Card>
        ))
      )}
    </section>
  )
}
