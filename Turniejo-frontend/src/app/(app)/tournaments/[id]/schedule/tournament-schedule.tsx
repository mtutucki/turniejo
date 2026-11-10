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
import {
  type ActiveFilter,
  ActiveFilters,
  ALL,
  FilterGrid,
  FilterOption,
  FilterPanel,
  FilterSelect,
} from "@/features/public/components/filter-bar"
import { useTournamentMatchesRealtime } from "@/features/matches/use-tournament-matches-realtime"
import { useTournamentMembers, useTournamentTeams } from "@/features/teams/api"
import { useCategories, useGroups } from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { formatDayLong, formatTime, plural, toZonedDateInput } from "@/lib/format"
import { cn } from "@/lib/utils"
import { type Match, type Pitch, SCHEDULABLE_STATUSES } from "@/types/matches"
import type { TournamentMember } from "@/types/teams"
import { UserRole } from "@/types/roles"

import { resultNote } from "@/features/matches/result-note"
import { hasScheduleDraft } from "./generate/generate-schedule"

const NO_REFEREE = "none"

type Lookups = {
  teamName: (tournamentTeamId: string) => string
  pitchName: (pitchId: string | null) => string | null
  groupLabel: (match: Match) => string | null
  referee: (userId: string | null) => TournamentMember | undefined
}

function RefereeChip({
  refereeId,
  referee,
  actionable,
}: {
  refereeId: string | null
  referee: TournamentMember | undefined
  /** Mecz jeszcze się nie skończył — brak sędziego wymaga uwagi organizatora. */
  actionable: boolean
}) {
  if (refereeId) {
    return (
      <span className="bg-primary/15 text-primary flex items-center gap-1 rounded-full px-2 py-0.5 font-medium">
        <Whistle className="size-3" />
        {/* Sędzia mógł opuścić turniej — przypisanie zostaje, choć nie znamy już nazwiska. */}
        {referee ? memberName(referee) : "Sędzia przypisany"}
      </span>
    )
  }
  if (!actionable) {
    return (
      <span className="flex items-center gap-1">
        <UserRound className="size-3" />
        Bez sędziego
      </span>
    )
  }
  // Bursztyn (--live) jest zarezerwowany dla trwających meczów — brak sędziego to obrys, nie alarm.
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          tabIndex={0}
          className="border-muted-foreground/60 text-foreground flex items-center gap-1 rounded-full border border-dashed px-2 py-0.5"
        >
          <UserRound className="size-3" />
          Bez sędziego
        </span>
      </TooltipTrigger>
      <TooltipContent>
        Mecz może wziąć dowolny sędzia turnieju albo poprowadzić go organizator.
      </TooltipContent>
    </Tooltip>
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
  const inProgress = match.status === "Live" || match.status === "Paused"
  const showScore =
    match.status === "Live" || match.status === "Paused" || match.status === "Finished"

  return (
    <li className="flex flex-col gap-3 py-3 lg:flex-row lg:items-center">
      <div className="flex min-w-0 flex-1 items-start gap-4">
        {/* Para bez terminu nie ma godziny, boiska ani sędziego — nie udajemy ich zaślepkami. */}
        {!unscheduled && (
          <span className="w-12 shrink-0 pt-0.5 font-mono text-sm font-semibold tabular-nums">
            {formatTime(match.scheduledAt)}
          </span>
        )}
        <div className="min-w-0 flex-1 space-y-1">
          <p className="flex flex-wrap items-center gap-x-2 font-medium">
            <span className="truncate">{home}</span>
            {showScore ? (
              <span className="font-mono tabular-nums">
                {match.homeScore}:{match.awayScore}
                {resultNote(match) && (
                  <span className="text-muted-foreground ml-1 font-sans text-xs">
                    ({resultNote(match)})
                  </span>
                )}
              </span>
            ) : (
              <span className="text-muted-foreground text-sm">vs</span>
            )}
            <span className="truncate">{away}</span>
          </p>
          <p className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
            {!unscheduled && (
              <>
                <MatchStatusBadge status={match.status} awaitingDecision={match.awaitingDecision} />
                <span className="flex items-center gap-1">
                  <MapPin className="size-3" />
                  {pitch ?? "Bez boiska"}
                </span>
              </>
            )}
            {group && <span>{group}</span>}
            {isOrganizer && !unscheduled && (
              <RefereeChip
                refereeId={match.refereeId}
                referee={referee}
                actionable={editable || inProgress}
              />
            )}
          </p>
        </div>
      </div>

      {isOrganizer && !unscheduled && (
        <div className="flex flex-wrap items-center gap-2 pl-16 lg:pl-0">
          {editable && (
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
          )}
          {/* Sędziego da się zmienić także w trakcie meczu — prowadzący może nagle wypaść. */}
          {(editable || inProgress) && (
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
          )}
          {!editable && (
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
                {inProgress
                  ? "Mecz trwa - terminu i boiska nie można już zmienić."
                  : "Mecz się zakończył albo został odwołany - terminu, boiska i sędziego nie można już zmienić."}
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
function PlannedRow({
  tournamentId,
  planned,
  lookups,
  isOrganizer,
  pitches,
  referees,
  refereeCode,
  unscheduled = false,
}: {
  tournamentId: string
  planned: PlannedBracketMatch
  lookups: Lookups
  isOrganizer: boolean
  pitches: Pitch[]
  referees: TournamentMember[]
  refereeCode: string | null
  /** Terminarz jeszcze nie powstał — sama para, bez godziny i boiska. */
  unscheduled?: boolean
}) {
  const sideClass = (known: boolean) => cn("truncate", !known && "text-muted-foreground italic")
  const title = `${planned.stage}: ${planned.home.label} – ${planned.away.label}`
  const pitch = lookups.pitchName(planned.pitchId)
  return (
    <li
      className="flex flex-col gap-3 py-3 lg:flex-row lg:items-center"
      data-testid="planned-bracket-match"
    >
      <div className="flex min-w-0 flex-1 items-start gap-4">
        {!unscheduled && (
          <span className="text-muted-foreground w-12 shrink-0 pt-0.5 font-mono text-sm font-semibold tabular-nums">
            {formatTime(planned.scheduledAt)}
          </span>
        )}
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
            {!unscheduled && (
              <span className="flex items-center gap-1">
                <MapPin className="size-3" />
                {pitch ?? "Bez boiska"}
              </span>
            )}
            <span>{planned.stage}</span>
            {isOrganizer && !unscheduled && (
              <RefereeChip
                refereeId={planned.refereeId}
                referee={lookups.referee(planned.refereeId)}
                actionable
              />
            )}
          </p>
        </div>
      </div>

      {/* Termin i sędzia zapisują się na drabince; mecz dostanie je, gdy będzie wiadomo, kto gra. */}
      {isOrganizer && !unscheduled && (
        <div className="flex flex-wrap items-center gap-2 pl-16 lg:pl-0">
          <RescheduleMatchDialog
            tournamentId={tournamentId}
            planned={planned}
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
            planned={planned}
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
      )}
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
                      ? "Terminy wyczyszczone - pary czekają na nowy terminarz"
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
  // Sędziowie biorą i oddają mecze, wyniki się zmieniają: terminarz odświeża się sam.
  useTournamentMatchesRealtime([id])
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
  const [teamId, setTeamId] = useState(ALL)
  const [groupId, setGroupId] = useState(ALL)
  const [pitchId, setPitchId] = useState(ALL)
  const [refereeId, setRefereeId] = useState(ALL)

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
          categoryById.size > 1 && m.categoryId && categoryById.get(m.categoryId),
          m.groupId ? groupById.get(m.groupId) : m.stageType !== "Group" && "Faza pucharowa",
        ]
          .filter(Boolean)
          .join(" · ") || null,
      referee: (uid) => (uid ? memberById.get(uid) : undefined),
    }
  }, [teams.data, pitches.data, groups.data, categories.data, members.data])

  const referees = (members.data ?? []).filter((m) => m.roles.includes(UserRole.Referee))
  // Przy kilku kategoriach i bez wybranej kategorii „Grupa B” byłaby niejednoznaczna.
  const categoryById = new Map((categories.data ?? []).map((c) => [c.id, c.name]))
  const withCategory = (name: string, cid: string) =>
    categoryById.size > 1 && categoryId === ALL ? `${categoryById.get(cid) ?? ""} · ${name}` : name
  const inCategory = (cid: string) => categoryId === ALL || cid === categoryId
  const groupOptions = (groups.data ?? [])
    .filter((g) => inCategory(g.categoryId))
    .map((g) => ({ id: g.id, name: withCategory(g.name, g.categoryId) }))
  // Filtr drużyny działa na drużynie z bazy: zgłoszona do kilku kategorii jest na liście raz,
  // a filtr pokazuje jej mecze ze wszystkich (albo z wybranej) kategorii.
  const baseTeamOf = new Map((teams.data ?? []).map((t) => [t.id, t.teamId]))
  const teamOptions = [
    ...new Map(
      (teams.data ?? [])
        .filter((t) => inCategory(t.categoryId))
        .map((t) => [t.teamId, { id: t.teamId, name: t.teamName }]),
    ).values(),
  ].sort((a, b) => a.name.localeCompare(b.name, "pl", { numeric: true }))
  const isTeam = (tournamentTeamId: string | undefined) =>
    !!tournamentTeamId && baseTeamOf.get(tournamentTeamId) === teamId

  const filtered = (schedule.data ?? [])
    .filter(
      (m) =>
        (categoryId === ALL || m.categoryId === categoryId) &&
        (teamId === ALL || isTeam(m.homeTournamentTeamId) || isTeam(m.awayTournamentTeamId)) &&
        (groupId === ALL || m.groupId === groupId) &&
        (pitchId === ALL || m.pitchId === pitchId) &&
        (refereeId === ALL || (m.refereeId ?? NO_REFEREE) === refereeId),
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
  // Mecze, które jeszcze się nie skończyły, a nikt ich nie prowadzi — organizator powinien je obsadzić.
  const matchesWithoutReferee = (schedule.data ?? []).filter(
    (m) =>
      !m.refereeId &&
      (SCHEDULABLE_STATUSES.includes(m.status) || m.status === "Live" || m.status === "Paused"),
  ).length
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

  // Mecze kolejnych rund pucharu, które jeszcze nie istnieją (czekają na zwycięzców). Przed
  // wygenerowaniem terminarza nie mają terminu — pokazujemy je i tak, żeby było widać całą drabinkę.
  const allPlanned = brackets.flatMap((bracket, i) =>
    bracket.data
      ? plannedBracketMatches(
          bracket.data,
          bracketCategoryIds[i]!,
          lookups.teamName,
          awaitingSchedule,
        )
      : [],
  )
  const planned = allPlanned.filter(
    (p) =>
      (categoryId === ALL || p.categoryId === categoryId) &&
      (teamId === ALL || isTeam(p.home.tournamentTeamId) || isTeam(p.away.tournamentTeamId)) &&
      groupId === ALL &&
      (refereeId === ALL || (p.refereeId ?? NO_REFEREE) === refereeId) &&
      (pitchId === ALL || p.pitchId === pitchId),
  )

  // Zapowiedziane mecze też da się obsadzić z góry, więc liczą się do "bez sędziego".
  const withoutReferee =
    matchesWithoutReferee + (awaitingSchedule ? 0 : allPlanned.filter((p) => !p.refereeId).length)

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

  const showCategoryFilter = (categories.data?.length ?? 0) > 1
  const showTeamFilter = teamOptions.length > 1
  // Puchar nie ma grup; pary bez terminu nie mają jeszcze boisk.
  const showGroupFilter = !knockout && groupOptions.length > 1
  const showPitchFilter = !awaitingSchedule && (pitches.data?.length ?? 0) > 1
  const showRefereeFilter = isOrganizer && !awaitingSchedule

  const refereeOptions = [
    { id: NO_REFEREE, name: "Bez sędziego" },
    ...referees.map((r) => ({ id: r.userId, name: memberName(r) })),
  ]
  const options = (list: { id: string; name: string }[]) =>
    list.map((o) => (
      <FilterOption key={o.id} value={o.id}>
        {o.name}
      </FilterOption>
    ))
  const filterSelects = [
    showTeamFilter && (
      <FilterSelect
        key="team"
        label="Drużyna"
        value={teamId}
        onChange={setTeamId}
        allLabel="Wszystkie drużyny"
      >
        {options(teamOptions)}
      </FilterSelect>
    ),
    showCategoryFilter && (
      <FilterSelect
        key="category"
        label="Kategoria"
        value={categoryId}
        onChange={(value) => {
          setCategoryId(value)
          setGroupId(ALL)
          // Drużyna, której nie ma w tej kategorii, dałaby zawsze pustą listę.
          const playsThere = teams.data?.some((t) => t.teamId === teamId && t.categoryId === value)
          if (teamId !== ALL && value !== ALL && !playsThere) setTeamId(ALL)
        }}
        allLabel="Wszystkie kategorie"
      >
        {options(categories.data ?? [])}
      </FilterSelect>
    ),
    showGroupFilter && (
      <FilterSelect
        key="group"
        label="Grupa"
        value={groupId}
        onChange={setGroupId}
        allLabel="Wszystkie grupy"
      >
        {options(groupOptions)}
      </FilterSelect>
    ),
    showPitchFilter && (
      <FilterSelect
        key="pitch"
        label="Boisko"
        value={pitchId}
        onChange={setPitchId}
        allLabel="Wszystkie boiska"
      >
        {options(pitches.data ?? [])}
      </FilterSelect>
    ),
    showRefereeFilter && (
      <FilterSelect
        key="referee"
        label="Sędzia"
        value={refereeId}
        onChange={setRefereeId}
        allLabel="Wszyscy sędziowie"
      >
        {options(refereeOptions)}
      </FilterSelect>
    ),
  ].filter(Boolean)

  const nameOf = (list: { id: string; name: string }[], id: string, fallback: string) =>
    list.find((o) => o.id === id)?.name ?? fallback
  const activeFilters: ActiveFilter[] = [
    teamId !== ALL && {
      key: "team",
      label: nameOf(teamOptions, teamId, "Drużyna"),
      clear: () => setTeamId(ALL),
    },
    categoryId !== ALL && {
      key: "category",
      label: nameOf(categories.data ?? [], categoryId, "Kategoria"),
      clear: () => {
        setCategoryId(ALL)
        setGroupId(ALL)
      },
    },
    groupId !== ALL && {
      key: "group",
      label: nameOf(groupOptions, groupId, "Grupa"),
      clear: () => setGroupId(ALL),
    },
    pitchId !== ALL && {
      key: "pitch",
      label: nameOf(pitches.data ?? [], pitchId, "Boisko"),
      clear: () => setPitchId(ALL),
    },
    refereeId !== ALL && {
      key: "referee",
      label: nameOf(refereeOptions, refereeId, "Sędzia"),
      clear: () => setRefereeId(ALL),
    },
  ].filter((f): f is ActiveFilter => !!f)
  function clearFilters() {
    setTeamId(ALL)
    setCategoryId(ALL)
    setGroupId(ALL)
    setPitchId(ALL)
    setRefereeId(ALL)
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
              ? "Puchar: pary 1. rundy ułożą się same według rozstawienia, gdy wygenerujesz terminarz. Jeśli chcesz, możesz je wcześniej ułożyć sam."
              : "Wygeneruj terminarz, gdy zapisy są zamknięte, a drużyny przypisane do grup."
            : "Organizator nie wygenerował jeszcze terminarza."
        }
        action={
          canGenerate ? (
            <div className="flex flex-wrap justify-center gap-2">
              <Button asChild>
                <Link href={`/tournaments/${id}/schedule/generate`}>
                  <CalendarCheck />
                  Generuj terminarz
                </Link>
              </Button>
              {knockout && (
                <Button asChild variant="outline">
                  <Link href={`/tournaments/${id}/bracket`}>Ułóż pary sam</Link>
                </Button>
              )}
            </div>
          ) : undefined
        }
      />
    )
  }

  return (
    <section className="max-w-5xl space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">
            {awaitingSchedule ? "Pary z drabinki" : "Terminarz"}
          </h2>
          <p className="text-muted-foreground text-sm">
            {awaitingSchedule
              ? `${plural(rows.length, ["mecz czeka", "mecze czekają", "meczów czeka"])} na godziny i boiska - nadasz je, generując terminarz.`
              : plural(schedule.data.length, ["mecz", "mecze", "meczów"])}
            {isOrganizer && !awaitingSchedule && (
              <>
                {" · "}
                {withoutReferee === 0 ? (
                  "wszystkie mają sędziego"
                ) : (
                  <button
                    type="button"
                    className="text-foreground font-medium underline underline-offset-2"
                    onClick={() => setRefereeId(NO_REFEREE)}
                  >
                    {plural(withoutReferee, ["mecz", "mecze", "meczów"])} bez sędziego
                  </button>
                )}
              </>
            )}
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

      {/* Filtr z jedną opcją niczego nie filtruje — pokazujemy tylko te, w których jest wybór. */}
      {filterSelects.length > 0 && (
        <div className="space-y-3">
          <FilterPanel label="Filtry terminarza">
            <FilterGrid count={filterSelects.length}>{filterSelects}</FilterGrid>
          </FilterPanel>
          {activeFilters.length > 0 && (
            <ActiveFilters
              summary={`Znaleziono ${plural(rows.length, ["mecz", "mecze", "meczów"])}`}
              filters={activeFilters}
              onClearAll={clearFilters}
            />
          )}
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState
          icon={CalendarClock}
          title="Brak meczów dla wybranych filtrów"
          action={
            <Button variant="outline" onClick={clearFilters}>
              Wyczyść filtry
            </Button>
          }
        />
      ) : (
        [...days.entries()].map(([day, dayRows]) => (
          <Card key={day} className="gap-0 py-0">
            <CardContent className="px-4">
              {!awaitingSchedule && (
                <h3 className="text-muted-foreground border-b py-3 text-sm font-medium first-letter:uppercase">
                  {formatDayLong(dayRows[0]!.at)}
                </h3>
              )}
              <ul className="divide-y">
                {dayRows.map((row) =>
                  row.kind === "planned" ? (
                    <PlannedRow
                      key={row.planned.nodeId}
                      tournamentId={id}
                      planned={row.planned}
                      lookups={lookups}
                      isOrganizer={isOrganizer}
                      pitches={pitches.data ?? []}
                      referees={referees}
                      refereeCode={tournament.data?.refereeCode ?? null}
                      unscheduled={awaitingSchedule}
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
