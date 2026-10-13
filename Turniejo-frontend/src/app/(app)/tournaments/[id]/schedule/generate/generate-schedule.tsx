"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { ArrowLeft, CalendarCheck, Check, ShieldAlert, X } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useEffect, useRef, useState } from "react"
import { useRouter } from "next/navigation"
import { Controller, useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { TextField } from "@/components/forms/text-field"
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
import { EmptyState } from "@/components/layout/empty-state"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from "@/components/ui/field"
import { Skeleton } from "@/components/ui/skeleton"
import { useGenerateSchedule, usePitches, useSavePitch, useSchedule } from "@/features/matches/api"
import { useAssignTeamToGroup, useTournamentTeams } from "@/features/teams/api"
import {
  useCategories,
  useChangeTournamentFormat,
  useChangeTournamentStatus,
  useGroups,
  useSaveGroup,
  useTournamentSettings,
} from "@/features/tournaments/api"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { applyFieldErrors } from "@/lib/api/form-errors"
import {
  formatDateTime,
  formatDayLong,
  formatTime,
  plural,
  toDateTimeLocal,
  toZonedDateInput,
  zonedToIso,
} from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match, Pitch } from "@/types/matches"
import type { TournamentTeam } from "@/types/teams"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentCategory,
  type TournamentFormat,
  type TournamentGroup,
  type TournamentSettings,
} from "@/types/tournaments"

const schema = z
  .object({
    // Godziny wspólne dla wszystkich dni — organizator ustawia je raz, wyjątki przy konkretnym dniu.
    commonStart: z.string(),
    commonEnd: z.string(),
    // "sequential": dni wypełniane po kolei; "even": po tyle samo meczów każdego wybranego dnia.
    mode: z.enum(["sequential", "even"]),
    // Jeden wiersz na każdy dzień turnieju; wyłączony dzień nie dostaje meczów.
    days: z.array(
      z.object({
        date: z.string(),
        enabled: z.boolean(),
        // Dzień ma własne godziny — zmiana wspólnych już go nie rusza.
        custom: z.boolean(),
        startTime: z.string(),
        endTime: z.string(),
      }),
    ),
    pitchIds: z.array(z.string()).min(1, "Wybierz co najmniej jedno boisko"),
  })
  .superRefine((values, ctx) => {
    values.days.forEach((day, index) => {
      if (!day.enabled) return
      if (!day.startTime)
        ctx.addIssue({
          code: "custom",
          path: ["days", index, "startTime"],
          message: "Podaj godzinę rozpoczęcia",
        })
      else if (!day.endTime)
        ctx.addIssue({
          code: "custom",
          path: ["days", index, "endTime"],
          message: "Podaj godzinę zakończenia",
        })
      else if (day.endTime <= day.startTime)
        ctx.addIssue({
          code: "custom",
          path: ["days", index, "endTime"],
          message: "Koniec musi być po rozpoczęciu",
        })
    })
  })

type GenerateValues = z.infer<typeof schema>
type DayValues = GenerateValues["days"][number]

/** Szkic formularza w sessionStorage — knownPitchIds odróżnia boisko odznaczone od dodanego później. */
type GenerateDraft = GenerateValues & { knownPitchIds: string[] }

const draftKey = (tournamentId: string) => `turniejo:schedule-draft:${tournamentId}`

/** Organizator otworzył generator i go nie zamknął — sekcja "Mecze" wraca wtedy prosto do formularza. */
export function hasScheduleDraft(tournamentId: string) {
  try {
    return sessionStorage.getItem(draftKey(tournamentId)) !== null
  } catch {
    return false
  }
}

function clearScheduleDraft(tournamentId: string) {
  try {
    sessionStorage.removeItem(draftKey(tournamentId))
  } catch {
    // Brak sessionStorage — nie ma czego czyścić.
  }
}

function readDraft(tournamentId: string): GenerateDraft | null {
  try {
    const draft = JSON.parse(sessionStorage.getItem(draftKey(tournamentId)) ?? "null")
    // Szkic sprzed wspólnych godzin i trybu rozkładu nie ma tych pól — zaczynamy wtedy od zera.
    const valid =
      draft &&
      Array.isArray(draft.days) &&
      Array.isArray(draft.pitchIds) &&
      Array.isArray(draft.knownPitchIds) &&
      typeof draft.commonStart === "string" &&
      typeof draft.commonEnd === "string" &&
      (draft.mode === "sequential" || draft.mode === "even")
    return valid ? draft : null
  } catch {
    return null
  }
}

/** Formularz ma wiersz na dzień — bardzo długi turniej dostaje tylko pierwsze dni. */
const MAX_DAYS = 31

/** Kolejne dni turnieju ("RRRR-MM-DD", strefa turnieju) od pierwszego do ostatniego. */
function tournamentDays(minDate: string, maxDate: string) {
  const days: string[] = []
  const cursor = new Date(`${minDate}T12:00:00Z`)
  while (days.length < MAX_DAYS) {
    const day = cursor.toISOString().slice(0, 10)
    if (day > maxDate) break
    days.push(day)
    cursor.setUTCDate(cursor.getUTCDate() + 1)
  }
  return days
}

type DayEstimate = { matches: number; end: Date }

/** Ile slotów (mecz + przerwa) mieści się w godzinach dnia — mecz musi się zmieścić w całości. */
function dayCapacity(day: DayValues, settings: TournamentSettings) {
  if (!day.enabled || !day.startTime || day.endTime <= day.startTime) return 0
  const start = new Date(zonedToIso(day.date, day.startTime)).getTime()
  const end = new Date(zonedToIso(day.date, day.endTime)).getTime()
  const slotMs = (settings.matchDurationMinutes + settings.breakDurationMinutes) * 60_000
  return Math.max(
    0,
    Math.floor((end - start - settings.matchDurationMinutes * 60_000) / slotMs) + 1,
  )
}

/**
 * Tak samo jak generator w backendzie: sloty wypełniają kolejne dni po kolei. Zwraca szacunek dla
 * każdego dnia (null = dzień bez meczów) i liczbę slotów, na które zabrakło godzin.
 */
function estimateDays(
  days: DayValues[],
  slots: number,
  totalMatches: number,
  settings: TournamentSettings,
) {
  const matchMs = settings.matchDurationMinutes * 60_000
  const slotMs = (settings.matchDurationMinutes + settings.breakDurationMinutes) * 60_000
  let placed = 0
  let matchesSoFar = 0
  const perDay = days.map((day): DayEstimate | null => {
    const used = Math.min(slots - placed, dayCapacity(day, settings))
    if (used <= 0) return null
    placed += used
    // Mecze rozłożone proporcjonalnie do slotów — ostatni slot dnia bywa niepełny.
    const matchesUntilNow = Math.round((totalMatches * placed) / slots)
    const matches = matchesUntilNow - matchesSoFar
    matchesSoFar = matchesUntilNow
    const start = new Date(zonedToIso(day.date, day.startTime)).getTime()
    return { matches, end: new Date(start + (used - 1) * slotMs + matchMs) }
  })
  return { perDay, missingSlots: slots - placed }
}

const toMinutes = (time: string) => {
  const [h, m] = time.split(":").map(Number) as [number, number]
  return h * 60 + m
}
const toTime = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`

type TournamentBounds = { firstStart: string; lastEnd: string; count: number }

/**
 * Godziny dnia ze wspólnych godzin, przycięte do ram turnieju: pierwszy dzień nie zaczyna się przed
 * startem turnieju, ostatni nie kończy po jego końcu.
 */
function dayHours(index: number, start: string, end: string, bounds: TournamentBounds) {
  const isFirst = index === 0
  const isLast = index === bounds.count - 1
  let startTime = isFirst && start < bounds.firstStart ? bounds.firstStart : start
  const endTime = isLast && end > bounds.lastEnd ? bounds.lastEnd : end
  // Turniej kończy się ostatniego dnia przed wspólną godziną startu (np. o 8:00) — gramy od północy.
  if (isLast && !isFirst && endTime <= startTime) startTime = "00:00"
  return { startTime, endTime }
}

const isWeekend = (date: string) => [0, 6].includes(new Date(`${date}T12:00:00Z`).getUTCDay())

const MODES = [
  {
    value: "sequential",
    title: "Jak najwcześniej",
    hint: "Mecze wypełniają dni po kolei — turniej kończy się najszybciej, jak się da.",
  },
  {
    value: "even",
    title: "Równo na wybrane dni",
    hint: "Każdego dnia mniej więcej tyle samo meczów; dzień kończy się po ostatnim z nich.",
  },
] as const

/**
 * Okna czasowe, które trafią do generatora. Generator zawsze wypełnia dni po kolei, więc rozkład
 * "równo" robimy, skracając każdy dzień do jego części meczów. Ostatni dzień zostaje w pełnych
 * godzinach — przejmie mecze, których generator nie zmieścił wcześniej (szacunek slotów bywa za niski).
 */
function planWindows(
  days: DayValues[],
  mode: GenerateValues["mode"],
  slots: number,
  settings: TournamentSettings,
) {
  const active = days.filter((day) => dayCapacity(day, settings) > 0)
  if (mode !== "even" || active.length < 2 || slots === 0) return days
  const perDay = Math.ceil(slots / active.length)
  const slotMinutes = settings.matchDurationMinutes + settings.breakDurationMinutes
  const last = active.at(-1)
  return days.map((day) => {
    if (day === last || dayCapacity(day, settings) <= perDay) return day
    const end =
      toMinutes(day.startTime) + (perDay - 1) * slotMinutes + settings.matchDurationMinutes
    return { ...day, endTime: toTime(end) }
  })
}

type GroupPlan = { group: TournamentGroup; teams: number; matches: number; rounds: number }

/** Co generator rozłoży w czasie: mecze grupowe albo (puchar) mecze 1. rundy z drabinki. */
type Workload = { totalMatches: number; maxRounds: number }

const isKnockout = (t: Tournament) => t.format === "Knockout"
const hasGroupStage = (t: Tournament) => t.format === "Group" || t.format === "GroupAndKnockout"

/** Mecze drabinki czekające na termin — to samo co ScheduleKnockout w backendzie. */
const pendingBracketMatches = (matches: Match[]) =>
  matches.filter(
    (m) =>
      m.stageType !== "Group" &&
      m.stageType !== "Friendly" &&
      (m.status === "Scheduled" || m.status === "Ready" || m.status === "Postponed"),
  )

/** Te same reguły co GenerateScheduleCommandHandler: grupa gra, jeśli ma ≥ 2 zaakceptowane drużyny. */
function planGroups(groups: TournamentGroup[], teams: TournamentTeam[]): GroupPlan[] {
  return groups.map((group) => {
    const n = teams.filter(
      (t) => t.groupId === group.id && t.registrationStatus === "Accepted",
    ).length
    return {
      group,
      teams: n,
      matches: n >= 2 ? (n * (n - 1)) / 2 : 0,
      // Metoda kołowa: n-1 kolejek dla parzystej liczby drużyn, n dla nieparzystej.
      rounds: n >= 2 ? (n % 2 === 0 ? n - 1 : n) : 0,
    }
  })
}

function Requirement({ ok, children }: { ok: boolean; children: ReactNode }) {
  return (
    <li className="flex items-start gap-2 text-sm">
      {ok ? (
        <Check className="text-primary mt-0.5 size-4 shrink-0" aria-label="Spełnione" />
      ) : (
        <X className="text-destructive mt-0.5 size-4 shrink-0" aria-label="Niespełnione" />
      )}
      <span className={cn(!ok && "text-foreground")}>{children}</span>
    </li>
  )
}

/**
 * Najczęstsza pułapka małego turnieju: drużyny są zaakceptowane, ale kategoria nie ma żadnej grupy
 * (albo drużyny nie są do niej przypisane). Jednym kliknięciem tworzymy "Grupa A" i przypisujemy
 * do niej wszystkie zaakceptowane drużyny kategorii bez grupy.
 */
function QuickGroupSetup({
  tournamentId,
  categoryId,
  categoryName,
  teams,
}: {
  tournamentId: string
  categoryId: string
  categoryName: string | null
  teams: TournamentTeam[]
}) {
  const saveGroup = useSaveGroup(tournamentId)
  const assign = useAssignTeamToGroup(tournamentId)
  const [pending, setPending] = useState(false)

  async function run() {
    setPending(true)
    try {
      const created = await saveGroup.mutateAsync({ categoryId, name: "Grupa A", order: 1 })
      const groupId = (created as { tournamentGroupId: string }).tournamentGroupId
      for (const team of teams) {
        await assign.mutateAsync({ tournamentTeamId: team.id, groupId })
      }
      toast.success(
        `Utworzono „Grupa A” z ${plural(teams.length, ["drużyną", "drużynami", "drużynami"])}`,
      )
    } finally {
      setPending(false)
    }
  }

  return (
    <Button
      type="button"
      size="sm"
      variant="outline"
      className="mt-2"
      onClick={run}
      disabled={pending}
    >
      {pending
        ? "Tworzenie…"
        : `Utwórz „Grupa A”${categoryName ? ` (${categoryName})` : ""} i przypisz ${plural(teams.length, ["drużynę", "drużyny", "drużyn"])}`}
    </Button>
  )
}

/** Następny krok zapisów prosto z listy warunków: Szkic → otwarte → zamknięte (z potwierdzeniem). */
function RegistrationAction({ tournament }: { tournament: Tournament }) {
  const change = useChangeTournamentStatus(tournament.id)
  if (tournament.status === "Draft") {
    return (
      <Button
        type="button"
        size="sm"
        variant="outline"
        className="mt-2"
        disabled={change.isPending}
        onClick={() =>
          change.mutate("RegistrationOpen", { onSuccess: () => toast.success("Zapisy otwarte") })
        }
      >
        Otwórz zapisy
      </Button>
    )
  }
  if (tournament.status !== "RegistrationOpen") return null
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="mt-2"
          disabled={change.isPending}
        >
          Zamknij zapisy
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Zamknąć zapisy?</AlertDialogTitle>
          <AlertDialogDescription>
            Po zamknięciu nie zgłosisz już nowych drużyn. Do wygenerowania terminarza zapisy można
            otworzyć ponownie w podsumowaniu turnieju.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Anuluj</AlertDialogCancel>
          <AlertDialogAction
            onClick={() =>
              change.mutate("RegistrationClosed", {
                onSuccess: () => toast.success("Zapisy zamknięte"),
              })
            }
          >
            Zamknij zapisy
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

const FORMAT_HINTS: Record<TournamentFormat, string> = {
  Group: "każdy z każdym w grupach",
  Knockout: "pary i drabinka",
  GroupAndKnockout: "grupy, potem drabinka",
}

/** Format wybierany od razu tutaj — backend przy formacie z grupami tworzy "Grupa A" w kategoriach. */
function FormatAction({ tournamentId }: { tournamentId: string }) {
  const change = useChangeTournamentFormat(tournamentId)
  return (
    <div className="mt-2 flex flex-wrap gap-2" role="group" aria-label="Format turnieju">
      {(Object.keys(TOURNAMENT_FORMAT_LABELS) as TournamentFormat[]).map((format) => (
        <Button
          key={format}
          type="button"
          size="sm"
          variant="outline"
          disabled={change.isPending}
          onClick={() =>
            change.mutate(format, {
              onSuccess: () => toast.success(`Format: ${TOURNAMENT_FORMAT_LABELS[format]}`),
            })
          }
        >
          {TOURNAMENT_FORMAT_LABELS[format]}
          <span className="text-muted-foreground text-xs font-normal">{FORMAT_HINTS[format]}</span>
        </Button>
      ))}
    </div>
  )
}

/** Pierwsze boisko jednym kliknięciem — nazwę zmienisz w zakładce Boiska. */
function PitchAction({ tournamentId }: { tournamentId: string }) {
  const save = useSavePitch(tournamentId)
  return (
    <Button
      type="button"
      size="sm"
      variant="outline"
      className="mt-2"
      disabled={save.isPending}
      onClick={() =>
        save.mutate(
          { name: "Boisko 1", description: null, isActive: true },
          { onSuccess: () => toast.success("Dodano „Boisko 1”") },
        )
      }
    >
      Dodaj „Boisko 1”
    </Button>
  )
}

function Requirements({
  tournament,
  plan,
  bracketMatches,
  activePitches,
  teams,
  groups,
  categories,
}: {
  tournament: Tournament
  plan: GroupPlan[]
  bracketMatches: number
  activePitches: number
  teams: TournamentTeam[]
  groups: TournamentGroup[]
  categories: TournamentCategory[]
}) {
  const id = tournament.id
  const playable = plan.filter((p) => p.matches > 0)
  const incomplete = plan.filter((p) => p.matches === 0)
  // Diagnoza "dlaczego grupa nie gra": drużyny bez grupy i czekające na akceptację.
  const accepted = teams.filter((t) => t.registrationStatus === "Accepted")
  const unassigned = accepted.filter((t) => !t.groupId)
  const waiting = teams.filter((t) => t.registrationStatus === "Pending")
  // Kategorie bez żadnej grupy, w których są co najmniej 2 zaakceptowane drużyny bez grupy.
  const quickSetup = categories
    .filter((c) => !groups.some((g) => g.categoryId === c.id))
    .map((c) => ({ category: c, teams: unassigned.filter((t) => t.categoryId === c.id) }))
    .filter((x) => x.teams.length >= 2)
  return (
    <ul className="space-y-2">
      <Requirement ok={tournament.status === "RegistrationClosed"}>
        Zapisy zamknięte
        {tournament.status !== "RegistrationClosed" && (
          <span className="text-muted-foreground block text-xs">
            Terminarz układa się dla ostatecznej listy drużyn — najpierw zamknij zapisy.
          </span>
        )}
        <RegistrationAction tournament={tournament} />
      </Requirement>
      <Requirement ok={!!tournament.format}>
        {tournament.format
          ? `Format: ${TOURNAMENT_FORMAT_LABELS[tournament.format]}`
          : "Format turnieju — wybierz, jak grają drużyny:"}
        {!tournament.format && <FormatAction tournamentId={id} />}
      </Requirement>
      {isKnockout(tournament) ? (
        <>
          <Requirement ok={accepted.length >= 2}>
            {accepted.length >= 2
              ? plural(accepted.length, [
                  "zaakceptowana drużyna",
                  "zaakceptowane drużyny",
                  "zaakceptowanych drużyn",
                ])
              : "Co najmniej 2 zaakceptowane drużyny"}
            {accepted.length < 2 && (
              <span className="text-muted-foreground block text-xs">
                {accepted.length === 0
                  ? "Turniej nie ma jeszcze żadnej zaakceptowanej drużyny"
                  : "Turniej ma tylko jedną zaakceptowaną drużynę"}{" "}
                — drabinka nie ma z kogo ułożyć par.{" "}
                <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
                  Przejdź do Drużyn
                </Link>
              </span>
            )}
            {waiting.length > 0 && (
              <span className="text-muted-foreground block text-xs">
                {plural(waiting.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])} na
                akceptację zgłoszenia — nie zagra, dopóki jej nie zaakceptujesz.
              </span>
            )}
          </Requirement>
          <Requirement ok={bracketMatches > 0}>
            {bracketMatches > 0 ? (
              `Drabinka gotowa — ${plural(bracketMatches, ["mecz", "mecze", "meczów"])} 1. rundy do rozstawienia`
            ) : accepted.length >= 2 ? (
              <>
                Drabinka z parami 1. rundy{" "}
                <Link href={`/tournaments/${id}/bracket`} className="text-primary underline">
                  (utwórz drabinkę)
                </Link>
              </>
            ) : (
              <>
                Drabinka z parami 1. rundy
                <span className="text-muted-foreground block text-xs">
                  Utworzysz ją, gdy turniej będzie miał co najmniej 2 zaakceptowane drużyny.
                </span>
              </>
            )}
          </Requirement>
        </>
      ) : (
        <Requirement ok={playable.length > 0}>
          {playable.length > 0
            ? `${plural(playable.length, ["grupa", "grupy", "grup"])} z co najmniej 2 zaakceptowanymi drużynami`
            : "Co najmniej jedna grupa z 2 zaakceptowanymi drużynami"}
          {incomplete.length > 0 && (
            <span className="text-muted-foreground block text-xs">
              Pominięte (mniej niż 2 drużyny): {incomplete.map((p) => p.group.name).join(", ")}
            </span>
          )}
          {!tournament.format && (
            <span className="text-muted-foreground block text-xs">
              Po wyborze formatu z grupami „Grupa A” powstanie sama, a drużyny trafią do niej
              automatycznie.
            </span>
          )}
          {tournament.format && groups.length === 0 && (
            <span className="text-muted-foreground block text-xs">
              Turniej nie ma jeszcze żadnej grupy — mecze grają drużyny przypisane do tej samej
              grupy.{" "}
              <Link href={`/tournaments/${id}/categories`} className="text-primary underline">
                Dodaj grupę
              </Link>
            </span>
          )}
          {unassigned.length > 0 && groups.length > 0 && (
            <span className="text-muted-foreground block text-xs">
              {plural(unassigned.length, [
                "zaakceptowana drużyna nie ma",
                "zaakceptowane drużyny nie mają",
                "zaakceptowanych drużyn nie ma",
              ])}{" "}
              grupy: {unassigned.map((t) => t.teamName).join(", ")}.{" "}
              <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
                Przypisz w Drużynach
              </Link>
            </span>
          )}
          {waiting.length > 0 && (
            <span className="text-muted-foreground block text-xs">
              {plural(waiting.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])} na
              akceptację zgłoszenia — nie zagra, dopóki jej nie zaakceptujesz.
            </span>
          )}
          {tournament.format &&
            playable.length === 0 &&
            quickSetup.map(({ category, teams: categoryTeams }) => (
              <QuickGroupSetup
                key={category.id}
                tournamentId={id}
                categoryId={category.id}
                categoryName={categories.length > 1 ? category.name : null}
                teams={categoryTeams}
              />
            ))}
        </Requirement>
      )}
      <Requirement ok={activePitches > 0}>
        {activePitches > 0 ? (
          plural(activePitches, ["aktywne boisko", "aktywne boiska", "aktywnych boisk"])
        ) : (
          <>
            Co najmniej jedno aktywne boisko
            <PitchAction tournamentId={id} />
          </>
        )}
      </Requirement>
    </ul>
  )
}

function GenerateForm({
  tournament,
  settings,
  pitches,
  workload,
  ready,
}: {
  tournament: Tournament
  settings: TournamentSettings
  pitches: Pitch[]
  workload: Workload
  ready: boolean
}) {
  const router = useRouter()
  const generate = useGenerateSchedule(tournament.id)
  // Szkic przeżywa przejście do innej sekcji (np. "zmień" czas meczu w ustawieniach) i powrót.
  const [draft] = useState(() => readDraft(tournament.id))
  // Turniej wyznacza ramy: pierwszy dzień nie zaczyna się przed jego startem, ostatni nie kończy po
  // jego końcu. Wspólne godziny i godziny dni liczymy raz, przy otwarciu formularza.
  const [initial] = useState(() => {
    const dates = tournamentDays(
      toZonedDateInput(tournament.startAt),
      toZonedDateInput(tournament.endAt),
    )
    const bounds: TournamentBounds = {
      firstStart: toDateTimeLocal(tournament.startAt).slice(11),
      lastEnd: toDateTimeLocal(tournament.endAt).slice(11),
      count: dates.length,
    }
    const commonStart = draft?.commonStart ?? bounds.firstStart
    const commonEnd =
      draft?.commonEnd ??
      (bounds.lastEnd > commonStart ? bounds.lastEnd : commonStart < "18:00" ? "18:00" : "23:59")
    const days = dates.map((date, index): DayValues => {
      const saved = draft?.days.find((day) => day.date === date)
      // Własne godziny dnia ze szkicu zostają, o ile nadal mieszczą się w datach turnieju.
      if (
        saved?.custom &&
        (index > 0 || saved.startTime >= bounds.firstStart) &&
        (index < dates.length - 1 || saved.endTime <= bounds.lastEnd)
      )
        return saved
      return {
        date,
        enabled: saved?.enabled ?? true,
        custom: false,
        ...dayHours(index, commonStart, commonEnd, bounds),
      }
    })
    return { bounds, commonStart, commonEnd, days }
  })
  const { bounds } = initial
  const multiDay = initial.days.length > 1
  const form = useForm<GenerateValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      commonStart: initial.commonStart,
      commonEnd: initial.commonEnd,
      mode: draft?.mode ?? "sequential",
      days: initial.days,
      pitchIds: draft
        ? draft.pitchIds.filter((id) => pitches.some((p) => p.id === id))
        : pitches.map((p) => p.id),
    },
  })
  const { errors } = form.formState
  const [days, pitchIds, mode, commonStart, commonEnd] = useWatch({
    control: form.control,
    name: ["days", "pitchIds", "mode", "commonStart", "commonEnd"],
  })

  // Boisko dodane już po otwarciu strony (np. przyciskiem "Dodaj Boisko 1" w warunkach) ma być od
  // razu zaznaczone — defaultValues zapamiętały listę z chwili montowania formularza.
  const knownPitches = useRef(new Set(draft?.knownPitchIds ?? pitches.map((p) => p.id)))

  // Klucz zamiast tablicy: `pitches` to nowa tablica przy każdym renderze, a zapis po "Anuluj"
  // przywróciłby właśnie usunięty szkic.
  const pitchKey = pitches.map((p) => p.id).join(",")
  useEffect(() => {
    try {
      sessionStorage.setItem(
        draftKey(tournament.id),
        JSON.stringify({
          days,
          pitchIds,
          mode,
          commonStart,
          commonEnd,
          knownPitchIds: pitchKey ? pitchKey.split(",") : [],
        } satisfies GenerateDraft),
      )
    } catch {
      // Brak sessionStorage (tryb prywatny) — formularz działa bez szkicu.
    }
  }, [tournament.id, days, pitchIds, mode, commonStart, commonEnd, pitchKey])
  useEffect(() => {
    const added = pitches.filter((p) => !knownPitches.current.has(p.id)).map((p) => p.id)
    if (added.length === 0) return
    added.forEach((id) => knownPitches.current.add(id))
    form.setValue("pitchIds", [...form.getValues("pitchIds"), ...added], { shouldValidate: true })
  }, [pitches, form])

  const { totalMatches, maxRounds } = workload
  // Szacunek: tyle slotów, ile wymaga liczba boisk, ale nie mniej niż kolejek w największej grupie
  // (drużyna nie gra dwóch meczów naraz). Generator może potrzebować trochę więcej.
  const slotsFor = (pitchCount: number) =>
    pitchCount ? Math.max(Math.ceil(totalMatches / pitchCount), maxRounds) : 0
  const slots = slotsFor(pitchIds.length)
  // To, co faktycznie pójdzie do generatora: przy rozkładzie "równo" dni są przycięte do swojej części.
  const planned = planWindows(days, mode, slots, settings)
  const estimate = estimateDays(planned, slots, totalMatches, settings)
  const fits = estimate.missingSlots === 0
  const usedDays = estimate.perDay.filter(Boolean).length
  const lastEnd = estimate.perDay.findLast(Boolean)?.end
  const enabledDays = days.filter((day) => day.enabled).length
  const slotMinutes = settings.matchDurationMinutes + settings.breakDurationMinutes
  const hasWeekend = days.some((day) => isWeekend(day.date))
  const hasWeekday = days.some((day) => !isWeekend(day.date))

  /** Wspólne godziny trafiają do wszystkich dni, które nie mają własnych. */
  function applyCommonHours(start: string, end: string) {
    form.getValues("days").forEach((day, index) => {
      if (day.custom) return
      const hours = dayHours(index, start, end, bounds)
      form.setValue(`days.${index}.startTime`, hours.startTime, { shouldValidate: true })
      form.setValue(`days.${index}.endTime`, hours.endTime, { shouldValidate: true })
    })
  }

  function setCustomHours(index: number, custom: boolean) {
    form.setValue(`days.${index}.custom`, custom, { shouldDirty: true })
    if (custom) return
    const hours = dayHours(
      index,
      form.getValues("commonStart"),
      form.getValues("commonEnd"),
      bounds,
    )
    form.setValue(`days.${index}.startTime`, hours.startTime, { shouldValidate: true })
    form.setValue(`days.${index}.endTime`, hours.endTime, { shouldValidate: true })
  }

  function setEnabledDays(pick: (day: DayValues) => boolean) {
    form.getValues("days").forEach((day, index) =>
      form.setValue(`days.${index}.enabled`, pick(day), {
        shouldDirty: true,
        shouldValidate: true,
      }),
    )
  }

  function submit(values: GenerateValues) {
    const windowsOf = planWindows(
      values.days,
      values.mode,
      slotsFor(values.pitchIds.length),
      settings,
    )
    const playDays = windowsOf
      .map((day, index) => ({ day, index }))
      .filter(({ day }) => day.enabled)
    const first = playDays[0]
    const last = playDays.at(-1)
    if (!first || !last) {
      form.setError("root", { message: "Wybierz co najmniej jeden dzień gry" })
      return
    }
    const windows = playDays.map(({ day }) => ({
      startAt: zonedToIso(day.date, day.startTime),
      endAt: zonedToIso(day.date, day.endTime),
    }))
    const startAt = windows[0]!.startAt
    const endAt = windows.at(-1)!.endAt
    // Backend sprawdza to samo — tu tylko po to, żeby błąd pojawił się przy polach.
    if (startAt < new Date(tournament.startAt).toISOString()) {
      form.setError(`days.${first.index}.startTime`, {
        message: `Turniej zaczyna się ${formatDateTime(tournament.startAt)}`,
      })
      return
    }
    if (endAt > new Date(tournament.endAt).toISOString()) {
      form.setError(`days.${last.index}.endTime`, {
        message: `Turniej kończy się ${formatDateTime(tournament.endAt)}`,
      })
      return
    }
    generate.mutate(
      {
        startAt,
        endAt,
        windows,
        pitchIds: values.pitchIds,
      },
      {
        onSuccess: ({ matchesCreated }) => {
          clearScheduleDraft(tournament.id)
          toast.success(
            isKnockout(tournament)
              ? `Rozstawiono ${plural(matchesCreated, ["mecz", "mecze", "meczów"])} 1. rundy`
              : `Wygenerowano ${plural(matchesCreated, ["mecz", "mecze", "meczów"])}`,
          )
          router.push(`/tournaments/${tournament.id}/schedule`)
        },
        onError: (error) => applyFieldErrors(error, form),
      },
    )
  }

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate>
      <FieldGroup>
        {/* 1. Co układamy — stałe wejście do całego wyliczenia. */}
        <p className="bg-muted/40 rounded-lg border px-3 py-2 text-sm">
          <span className="font-medium">{plural(totalMatches, ["mecz", "mecze", "meczów"])}</span>{" "}
          do rozstawienia · mecz{" "}
          <span className="font-mono tabular-nums">{settings.matchDurationMinutes} min</span> +
          przerwa{" "}
          <span className="font-mono tabular-nums">{settings.breakDurationMinutes} min</span>{" "}
          <Link
            href={`/tournaments/${tournament.id}/settings#rules`}
            className="text-muted-foreground text-xs underline"
          >
            zmień
          </Link>
        </p>

        <Controller
          control={form.control}
          name="pitchIds"
          render={({ field }) => (
            <FieldSet data-invalid={!!errors.pitchIds}>
              <FieldLegend variant="label">Boiska</FieldLegend>
              <div className="flex flex-wrap gap-x-6 gap-y-2">
                {pitches.map((pitch) => {
                  const checked = field.value.includes(pitch.id)
                  return (
                    <Field key={pitch.id} orientation="horizontal" className="w-auto">
                      <Checkbox
                        id={`pitch-${pitch.id}`}
                        checked={checked}
                        onCheckedChange={(next) =>
                          field.onChange(
                            next
                              ? [...field.value, pitch.id]
                              : field.value.filter((v) => v !== pitch.id),
                          )
                        }
                      />
                      <FieldLabel htmlFor={`pitch-${pitch.id}`} className="font-normal">
                        {pitch.name}
                      </FieldLabel>
                    </Field>
                  )
                })}
              </div>
              <FieldError errors={[errors.pitchIds]} />
            </FieldSet>
          )}
        />

        {/* 2. Godziny — jedne dla wszystkich dni; wyjątki ustawia się przy konkretnym dniu. */}
        <FieldSet>
          <FieldLegend variant="label">
            {multiDay ? "Godziny gry (każdego dnia)" : "Godziny gry"}
          </FieldLegend>
          <div className="grid max-w-md grid-cols-2 gap-3">
            <TextField
              id="schedule-common-start"
              label="Pierwszy mecz"
              type="time"
              className="font-mono tabular-nums"
              error={multiDay ? undefined : errors.days?.[0]?.startTime}
              {...form.register("commonStart", {
                onChange: (e) => applyCommonHours(e.target.value, form.getValues("commonEnd")),
              })}
            />
            <TextField
              id="schedule-common-end"
              label="Koniec"
              type="time"
              className="font-mono tabular-nums"
              error={multiDay ? undefined : errors.days?.[0]?.endTime}
              {...form.register("commonEnd", {
                onChange: (e) => applyCommonHours(form.getValues("commonStart"), e.target.value),
              })}
            />
          </div>
          <FieldDescription>
            Ostatni mecz dnia musi się skończyć przed godziną końca. Turniej trwa{" "}
            {formatDateTime(tournament.startAt)} – {formatDateTime(tournament.endAt)}.
          </FieldDescription>
        </FieldSet>

        {/* 3. Dni — wybór i od razu wynik: ile meczów wypadnie którego dnia. */}
        {multiDay && (
          <FieldSet>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <FieldLegend variant="label" className="mb-0">
                Dni gry
              </FieldLegend>
              <div className="flex flex-wrap gap-2">
                {hasWeekend && hasWeekday && (
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => setEnabledDays((day) => !isWeekend(day.date))}
                  >
                    Bez weekendów
                  </Button>
                )}
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() => setEnabledDays(() => true)}
                >
                  Wszystkie dni
                </Button>
              </div>
            </div>
            <ul className="divide-y rounded-lg border">
              {days.map((day, index) => {
                const dayEstimate = estimate.perDay[index]
                const dayErrors = errors.days?.[index]
                const showInputs = day.enabled && (day.custom || !!dayErrors)
                return (
                  <li key={day.date} className="space-y-3 px-3 py-2.5">
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                      <Controller
                        control={form.control}
                        name={`days.${index}.enabled`}
                        render={({ field }) => (
                          <Field orientation="horizontal" className="w-auto min-w-0 flex-1">
                            <Checkbox
                              id={`schedule-day-${index}`}
                              checked={field.value}
                              onCheckedChange={(next) => field.onChange(next === true)}
                            />
                            <FieldLabel
                              htmlFor={`schedule-day-${index}`}
                              className={cn(
                                "font-normal first-letter:uppercase",
                                !day.enabled && "text-muted-foreground",
                              )}
                            >
                              {formatDayLong(`${day.date}T12:00:00Z`)}
                            </FieldLabel>
                          </Field>
                        )}
                      />
                      {day.enabled ? (
                        <>
                          <span
                            className={cn(
                              "text-sm",
                              dayEstimate ? "font-medium" : "text-muted-foreground",
                            )}
                          >
                            {dayEstimate ? (
                              <>
                                {plural(dayEstimate.matches, ["mecz", "mecze", "meczów"])}
                                <span className="text-muted-foreground font-mono font-normal tabular-nums">
                                  {" "}
                                  {day.startTime}–{formatTime(dayEstimate.end)}
                                </span>
                              </>
                            ) : slots === 0 ? (
                              `${day.startTime}–${day.endTime}`
                            ) : fits ? (
                              "wolny — mecze zmieszczą się wcześniej"
                            ) : (
                              "za krótki na mecz"
                            )}
                          </span>
                          <Button
                            type="button"
                            size="sm"
                            variant="link"
                            className="text-muted-foreground h-auto px-0"
                            onClick={() => setCustomHours(index, !day.custom)}
                          >
                            {day.custom ? "Wspólne godziny" : "Inne godziny"}
                          </Button>
                        </>
                      ) : (
                        <span className="text-muted-foreground text-sm">bez meczów</span>
                      )}
                    </div>
                    {showInputs && (
                      <div className="grid max-w-md grid-cols-2 gap-3 pl-7">
                        <TextField
                          id={`schedule-start-${index}`}
                          label="Pierwszy mecz"
                          type="time"
                          className="font-mono tabular-nums"
                          error={dayErrors?.startTime}
                          {...form.register(`days.${index}.startTime`, {
                            onChange: () => form.setValue(`days.${index}.custom`, true),
                          })}
                        />
                        <TextField
                          id={`schedule-end-${index}`}
                          label="Koniec"
                          type="time"
                          className="font-mono tabular-nums"
                          error={dayErrors?.endTime}
                          {...form.register(`days.${index}.endTime`, {
                            onChange: () => form.setValue(`days.${index}.custom`, true),
                          })}
                        />
                      </div>
                    )}
                  </li>
                )
              })}
            </ul>
          </FieldSet>
        )}

        {/* 4. Rozkład — bez tego organizator sam skracał dni, żeby mecze nie zbiły się na początku. */}
        {multiDay && enabledDays > 1 && (
          <Controller
            control={form.control}
            name="mode"
            render={({ field }) => (
              <FieldSet>
                <FieldLegend variant="label">Jak rozłożyć mecze</FieldLegend>
                <div className="grid gap-2 sm:grid-cols-2" role="radiogroup">
                  {MODES.map(({ value, title, hint }) => (
                    <button
                      key={value}
                      type="button"
                      role="radio"
                      aria-checked={field.value === value}
                      onClick={() => field.onChange(value)}
                      className={cn(
                        "focus-visible:ring-ring rounded-lg border p-3 text-left text-sm transition-colors outline-none focus-visible:ring-2",
                        field.value === value
                          ? "border-primary bg-primary/5"
                          : "hover:border-primary/50",
                      )}
                    >
                      <span className="block font-medium">{title}</span>
                      <span className="text-muted-foreground block text-xs">{hint}</span>
                    </button>
                  ))}
                </div>
              </FieldSet>
            )}
          />
        )}

        {/* 5. Wynik wyliczenia — czy się mieści, a jeśli nie, to ile brakuje. */}
        {slots > 0 && (
          <div
            role="status"
            className={cn(
              "rounded-lg border px-3 py-2.5 text-sm",
              fits ? "border-primary/40 bg-primary/5" : "border-destructive/50 bg-destructive/10",
            )}
          >
            {enabledDays === 0 ? (
              <p className="font-medium">Wybierz co najmniej jeden dzień gry.</p>
            ) : fits && lastEnd ? (
              <>
                <p className="font-medium">
                  Zmieści się
                  {multiDay &&
                    ` — mecze zajmą ${usedDays} z ${plural(enabledDays, ["wybranego dnia", "wybranych dni", "wybranych dni"])}`}
                  .
                </p>
                <p className="text-muted-foreground">
                  Ostatni mecz skończy się ok.{" "}
                  <span className="font-mono tabular-nums">
                    {multiDay ? formatDateTime(lastEnd) : formatTime(lastEnd)}
                  </span>
                  .
                </p>
              </>
            ) : (
              <>
                <p className="font-medium">
                  Zabraknie czasu na ok.{" "}
                  {plural(Math.min(totalMatches, estimate.missingSlots * pitchIds.length), [
                    "mecz",
                    "mecze",
                    "meczów",
                  ])}
                  .
                </p>
                <p className="text-muted-foreground">
                  Brakuje ok.{" "}
                  <span className="font-mono tabular-nums">
                    {estimate.missingSlots * slotMinutes} min
                  </span>{" "}
                  gry: wydłuż godziny{multiDay && ", włącz kolejny dzień"} albo dodaj boisko.
                </p>
              </>
            )}
          </div>
        )}
        <FieldDescription className="-mt-4">
          Godziny w czasie polskim. Drużyna nigdy nie gra dwóch meczów jednocześnie. To szacunek —
          dokładne terminy zobaczysz po wygenerowaniu i każdy mecz możesz potem przesunąć.
        </FieldDescription>

        <FieldError errors={[errors.root]} />

        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={!ready || generate.isPending}>
            <CalendarCheck />
            {generate.isPending ? "Generowanie…" : "Generuj terminarz"}
          </Button>
          <Button asChild variant="ghost">
            <Link
              href={`/tournaments/${tournament.id}/schedule`}
              onClick={() => clearScheduleDraft(tournament.id)}
            >
              Anuluj
            </Link>
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

export function GenerateScheduleView() {
  const { id, tournament, isOrganizer, rolesLoading } = useTournamentContext()
  const settings = useTournamentSettings(id)
  const pitches = usePitches(id)
  const groups = useGroups(id)
  const teams = useTournamentTeams(id)
  const schedule = useSchedule(id)
  const categories = useCategories(id)

  const backLink = (
    <Button asChild variant="ghost" size="sm" className="-ml-2">
      <Link href={`/tournaments/${id}/schedule`} onClick={() => clearScheduleDraft(id)}>
        <ArrowLeft />
        Terminarz
      </Link>
    </Button>
  )

  if (!rolesLoading && !isOrganizer) {
    return (
      <EmptyState
        icon={ShieldAlert}
        title="Tylko organizator generuje terminarz"
        action={backLink}
      />
    )
  }

  const loading =
    !tournament.data ||
    settings.isPending ||
    pitches.isPending ||
    groups.isPending ||
    teams.isPending ||
    schedule.isPending ||
    categories.isPending
  if (loading) return <Skeleton className="h-96 w-full max-w-3xl rounded-xl" />

  const t = tournament.data!
  const activePitches = (pitches.data ?? []).filter((p) => p.isActive)
  const plan = planGroups(groups.data ?? [], teams.data ?? [])
  const bracketMatches = pendingBracketMatches(schedule.data ?? []).length
  // Puchar: 1. runda to jedna "kolejka" — każda drużyna gra w niej raz.
  const workload: Workload = isKnockout(t)
    ? { totalMatches: bracketMatches, maxRounds: bracketMatches > 0 ? 1 : 0 }
    : {
        totalMatches: plan.reduce((sum, p) => sum + p.matches, 0),
        maxRounds: Math.max(0, ...plan.map((p) => p.rounds)),
      }
  if (t.status === "Cancelled") {
    return <EmptyState icon={ShieldAlert} title="Turniej został anulowany" action={backLink} />
  }

  const alreadyGenerated =
    t.status !== "Draft" && t.status !== "RegistrationOpen" && t.status !== "RegistrationClosed"

  if (alreadyGenerated) {
    return (
      <EmptyState
        icon={CalendarCheck}
        title="Terminarz jest już wygenerowany"
        description="Pojedyncze mecze przesuniesz w terminarzu (zmiana godziny i boiska)."
        action={backLink}
      />
    )
  }

  const ready =
    t.status === "RegistrationClosed" &&
    (isKnockout(t) ? bracketMatches > 0 : hasGroupStage(t) && plan.some((p) => p.matches > 0)) &&
    activePitches.length > 0

  return (
    <div className="max-w-3xl space-y-4">
      {backLink}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Warunki</CardTitle>
          <CardDescription>
            {isKnockout(t)
              ? "Puchar: najpierw ułóż pary 1. rundy w drabince — tu dostaną godziny i boiska. Kolejne rundy zaplanują się same po rozstrzygnięciu meczów."
              : "Mecze „każdy z każdym” w grupach, dla drużyn zaakceptowanych i przypisanych do grupy."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Requirements
            tournament={t}
            plan={plan}
            bracketMatches={bracketMatches}
            activePitches={activePitches.length}
            teams={teams.data ?? []}
            groups={groups.data ?? []}
            categories={categories.data ?? []}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Generuj terminarz</CardTitle>
          <CardDescription>
            Po wygenerowaniu turniej przejdzie do etapu „Harmonogram gotowy” — generowanie jest
            jednorazowe.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {settings.data && (
            <GenerateForm
              tournament={t}
              settings={settings.data}
              pitches={activePitches}
              workload={workload}
              ready={ready}
            />
          )}
        </CardContent>
      </Card>
    </div>
  )
}
