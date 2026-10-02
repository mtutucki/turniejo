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
    // Jeden wiersz na każdy dzień turnieju; wyłączony dzień nie dostaje meczów.
    days: z.array(
      z.object({
        date: z.string(),
        enabled: z.boolean(),
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
    const valid =
      draft &&
      Array.isArray(draft.days) &&
      Array.isArray(draft.pitchIds) &&
      Array.isArray(draft.knownPitchIds)
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
  // Godziny wyliczone z turnieju: pierwszy dzień od godziny rozpoczęcia, ostatni do godziny zakończenia,
  // dni pomiędzy w tych samych godzinach. Szkic wygrywa, o ile mieści się w datach turnieju
  // (mogły się zmienić od jego zapisania).
  const [initialDays] = useState(() => {
    const dates = tournamentDays(
      toZonedDateInput(tournament.startAt),
      toZonedDateInput(tournament.endAt),
    )
    const firstStart = toDateTimeLocal(tournament.startAt).slice(11)
    const lastEnd = toDateTimeLocal(tournament.endAt).slice(11)
    return dates.map((date, index): DayValues => {
      const isFirst = index === 0
      const isLast = index === dates.length - 1
      const saved = draft?.days.find((day) => day.date === date)
      if (
        saved &&
        (!isFirst || saved.startTime >= firstStart) &&
        (!isLast || saved.endTime <= lastEnd)
      )
        return saved
      const startTime = isFirst || !isLast || firstStart < lastEnd ? firstStart : "00:00"
      const endTime =
        isLast || lastEnd > startTime ? lastEnd : "18:00" > startTime ? "18:00" : "23:59"
      return { date, enabled: saved?.enabled ?? true, startTime, endTime }
    })
  })
  const multiDay = initialDays.length > 1
  const form = useForm<GenerateValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      days: initialDays,
      pitchIds: draft
        ? draft.pitchIds.filter((id) => pitches.some((p) => p.id === id))
        : pitches.map((p) => p.id),
    },
  })
  const { errors } = form.formState
  const [days, pitchIds] = useWatch({ control: form.control, name: ["days", "pitchIds"] })

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
          knownPitchIds: pitchKey ? pitchKey.split(",") : [],
        } satisfies GenerateDraft),
      )
    } catch {
      // Brak sessionStorage (tryb prywatny) — formularz działa bez szkicu.
    }
  }, [tournament.id, days, pitchIds, pitchKey])
  useEffect(() => {
    const added = pitches.filter((p) => !knownPitches.current.has(p.id)).map((p) => p.id)
    if (added.length === 0) return
    added.forEach((id) => knownPitches.current.add(id))
    form.setValue("pitchIds", [...form.getValues("pitchIds"), ...added], { shouldValidate: true })
  }, [pitches, form])

  const { totalMatches, maxRounds } = workload
  // Szacunek: tyle slotów, ile wymaga liczba boisk, ale nie mniej niż kolejek w największej grupie
  // (drużyna nie gra dwóch meczów naraz). Generator może potrzebować trochę więcej.
  const slots = pitchIds.length ? Math.max(Math.ceil(totalMatches / pitchIds.length), maxRounds) : 0
  const estimate = estimateDays(days, slots, totalMatches, settings)
  const fits = estimate.missingSlots === 0
  const usedDays = estimate.perDay.filter(Boolean).length
  const lastEnd = estimate.perDay.findLast(Boolean)?.end
  const enabledDays = days.filter((day) => day.enabled).length

  // Podpowiedź: tyle samo slotów każdego włączonego dnia, koniec dnia zaraz po ostatnim meczu.
  // Godziny rozpoczęcia zostają; dzień nie wychodzi poza północ ani poza koniec turnieju.
  function spreadEvenly() {
    const current = form.getValues("days")
    const active = current.filter((day) => day.enabled && day.startTime)
    if (active.length === 0 || slots === 0) return
    const perDaySlots = Math.ceil(slots / active.length)
    const slotMinutes = settings.matchDurationMinutes + settings.breakDurationMinutes
    const tournamentEnd = toDateTimeLocal(tournament.endAt)
    current.forEach((day, index) => {
      if (!day.enabled || !day.startTime) return
      const wanted =
        toMinutes(day.startTime) + (perDaySlots - 1) * slotMinutes + settings.matchDurationMinutes
      const limit =
        day.date === tournamentEnd.slice(0, 10) ? toMinutes(tournamentEnd.slice(11)) : 23 * 60 + 59
      form.setValue(`days.${index}.endTime`, toTime(Math.min(wanted, limit)), {
        shouldDirty: true,
        shouldValidate: true,
      })
    })
  }

  function submit(values: GenerateValues) {
    const playDays = values.days
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
        <FieldSet>
          <FieldLegend variant="label">{multiDay ? "Dni gry" : "Godziny gry"}</FieldLegend>
          <div className="space-y-4">
            {initialDays.map(({ date }, index) => {
              const enabled = days[index]?.enabled ?? true
              const dayLabel = formatDayLong(`${date}T12:00:00Z`)
              return (
                <div key={date} className="grid gap-3 sm:grid-cols-3 sm:items-start">
                  {multiDay ? (
                    <Controller
                      control={form.control}
                      name={`days.${index}.enabled`}
                      render={({ field }) => (
                        <Field orientation="horizontal" className="sm:pt-8">
                          <Checkbox
                            id={`schedule-day-${index}`}
                            checked={field.value}
                            onCheckedChange={(next) => field.onChange(next === true)}
                          />
                          <FieldLabel htmlFor={`schedule-day-${index}`} className="font-normal">
                            {dayLabel}
                          </FieldLabel>
                        </Field>
                      )}
                    />
                  ) : (
                    <p className="text-sm sm:pt-8">{dayLabel}</p>
                  )}
                  {enabled ? (
                    <>
                      <TextField
                        id={`schedule-start-${index}`}
                        label="Pierwszy mecz"
                        type="time"
                        className="font-mono tabular-nums"
                        error={errors.days?.[index]?.startTime}
                        {...form.register(`days.${index}.startTime`)}
                      />
                      <TextField
                        id={`schedule-end-${index}`}
                        label="Koniec"
                        type="time"
                        className="font-mono tabular-nums"
                        error={errors.days?.[index]?.endTime}
                        {...form.register(`days.${index}.endTime`)}
                      />
                      {multiDay && slots > 0 && (
                        <p className="text-muted-foreground -mt-1 text-xs sm:col-span-2 sm:col-start-2">
                          {estimate.perDay[index] ? (
                            <>
                              ok.{" "}
                              {plural(estimate.perDay[index].matches, ["mecz", "mecze", "meczów"])},
                              koniec ok.{" "}
                              <span className="font-mono tabular-nums">
                                {formatTime(estimate.perDay[index].end)}
                              </span>
                            </>
                          ) : fits ? (
                            "Wolny — wszystkie mecze zmieszczą się wcześniej"
                          ) : (
                            "Za krótki, żeby zmieścić mecz"
                          )}
                        </p>
                      )}
                    </>
                  ) : (
                    <p className="text-muted-foreground text-sm sm:col-span-2 sm:pt-8">
                      Bez meczów tego dnia
                    </p>
                  )}
                </div>
              )
            })}
          </div>
        </FieldSet>
        <FieldDescription className="-mt-4">
          Godziny wyliczone z dat turnieju ({formatDateTime(tournament.startAt)} –{" "}
          {formatDateTime(tournament.endAt)}) — możesz je dostosować. Ostatni mecz dnia musi się
          skończyć przed godziną końca.
          {multiDay &&
            " Mecze wypełniają kolejne dni po kolei — pod każdym dniem widać, ile ich wypadnie."}
        </FieldDescription>

        <Controller
          control={form.control}
          name="pitchIds"
          render={({ field }) => (
            <FieldSet data-invalid={!!errors.pitchIds}>
              <FieldLegend variant="label">Boiska</FieldLegend>
              <div className="grid gap-2 sm:grid-cols-2">
                {pitches.map((pitch) => {
                  const checked = field.value.includes(pitch.id)
                  return (
                    <Field key={pitch.id} orientation="horizontal">
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

        <div className="bg-muted/40 space-y-1 rounded-lg border p-3 text-sm">
          <p>
            <span className="font-medium">{plural(totalMatches, ["mecz", "mecze", "meczów"])}</span>{" "}
            · mecz{" "}
            <span className="font-mono tabular-nums">{settings.matchDurationMinutes} min</span> +
            przerwa{" "}
            <span className="font-mono tabular-nums">{settings.breakDurationMinutes} min</span>{" "}
            <Link
              href={`/tournaments/${tournament.id}/settings`}
              className="text-muted-foreground text-xs underline"
            >
              zmień
            </Link>
          </p>
          {slots > 0 && (
            <p className={cn("text-muted-foreground", !fits && "text-destructive")}>
              {fits && lastEnd ? (
                <>
                  {multiDay &&
                    `Mecze zajmą ${usedDays} z ${plural(initialDays.length, ["dnia", "dni", "dni"])} turnieju. `}
                  Szacowany koniec ok.{" "}
                  <span className="font-mono tabular-nums">
                    {multiDay ? formatDateTime(lastEnd) : formatTime(lastEnd)}
                  </span>
                </>
              ) : (
                <>
                  Zabraknie czasu na ok.{" "}
                  {plural(Math.min(totalMatches, estimate.missingSlots * pitchIds.length), [
                    "mecz",
                    "mecze",
                    "meczów",
                  ])}
                  . Dodaj boisko
                  {multiDay ? ", wydłuż dzień albo włącz kolejny" : " albo wydłuż dzień"}.
                </>
              )}
            </p>
          )}
          {multiDay && slots > 0 && enabledDays > 1 && (
            <Button type="button" size="sm" variant="outline" onClick={spreadEvenly}>
              Rozłóż równo na {plural(enabledDays, ["dzień", "dni", "dni"])}
            </Button>
          )}
          <FieldDescription>
            Godziny w czasie polskim. Drużyna nigdy nie gra dwóch meczów jednocześnie. Terminy można
            potem przesuwać pojedynczo.
          </FieldDescription>
        </div>

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
