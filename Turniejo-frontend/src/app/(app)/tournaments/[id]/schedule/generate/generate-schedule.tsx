"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import {
  ArrowLeft,
  CalendarCheck,
  Check,
  Info,
  Plus,
  ShieldAlert,
  TriangleAlert,
  X,
} from "lucide-react"
import Link from "next/link"
import { type ReactNode, useEffect, useRef, useState } from "react"
import { useRouter } from "next/navigation"
import { Controller, useFieldArray, useForm, useWatch } from "react-hook-form"
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
import {
  useBrackets,
  useGenerateBracket,
  useGenerateSchedule,
  usePitches,
  useSavePitch,
  useSchedule,
} from "@/features/matches/api"
import { bracketStages } from "@/features/matches/planned-bracket-matches"
import { autoBracketPairs } from "@/features/matches/suggest-bracket"
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
import { isApiError } from "@/lib/api/errors"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { formatDateTime, formatDayLong, plural, toDateTimeLocal, zonedToIso } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Match, Pitch } from "@/types/matches"
import type { Bracket } from "@/types/public"
import type { TournamentTeam } from "@/types/teams"
import {
  TOURNAMENT_FORMAT_LABELS,
  type Tournament,
  type TournamentCategory,
  type TournamentFormat,
  type TournamentGroup,
  type TournamentSettings,
} from "@/types/tournaments"

const schema = z.object({
  // Jeden wiersz na każdy dzień gry. Dni, których tu nie ma (np. weekend), zostają bez meczów.
  days: z
    .array(
      z.object({
        date: z.string().min(1, "Wybierz dzień"),
        startTime: z.string().min(1, "Podaj godzinę"),
        // Ile meczów gra się tego dnia — o tym decyduje organizator, nie godzina końca.
        matches: z.number({ error: "Podaj liczbę" }).int("Podaj liczbę całkowitą").min(1, "Min. 1"),
      }),
    )
    .min(1, "Dodaj co najmniej jeden dzień gry"),
  pitchIds: z.array(z.string()).min(1, "Wybierz co najmniej jedno boisko"),
})

type GenerateValues = z.infer<typeof schema>
type DayValues = GenerateValues["days"][number]

/** Szkic formularza w sessionStorage — knownPitchIds odróżnia boisko odznaczone od dodanego później. */
type GenerateDraft = GenerateValues & { knownPitchIds: string[] }

const draftKey = (tournamentId: string) => `turniejo:schedule-draft:${tournamentId}`

/**
 * Kategorie ("turniej:kategoria"), dla których już próbowaliśmy utworzyć drabinkę automatycznie.
 * Poza komponentem, bo useRef nie przeżywa ponownego zamontowania (StrictMode w dev montuje dwa razy)
 * i dwie drabinki szły do backendu równolegle.
 */
const autoBracketTried = new Set<string>()

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
    // Szkic ze starszej wersji formularza (godziny końca zamiast liczby meczów) — zaczynamy od zera.
    const valid =
      draft &&
      Array.isArray(draft.days) &&
      draft.days.every((day: Partial<DayValues>) => typeof day?.matches === "number") &&
      Array.isArray(draft.pitchIds) &&
      Array.isArray(draft.knownPitchIds)
    return valid ? draft : null
  } catch {
    return null
  }
}

const toMinutes = (time: string) => {
  const [h, m] = time.split(":").map(Number) as [number, number]
  return h * 60 + m
}
const toTime = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`

/** Ramy turnieju w jego strefie: gramy od startu, a ostatni mecz musi się skończyć przed końcem. */
type TournamentBounds = { startDate: string; startTime: string; endDate: string; endTime: string }

const nextDate = (date: string) => {
  const next = new Date(`${date}T12:00:00Z`)
  next.setUTCDate(next.getUTCDate() + 1)
  return next.toISOString().slice(0, 10)
}

/** Do której minuty dnia mogą trwać mecze: do północy, a ostatniego dnia — do końca turnieju. */
const dayLimit = (date: string, bounds: TournamentBounds) =>
  date === bounds.endDate ? toMinutes(bounds.endTime) : 24 * 60 - 1

/** Ile meczów zmieści się danego dnia od podanej godziny (mecz musi się zmieścić w całości). */
function dayCapacity(
  day: Pick<DayValues, "date" | "startTime">,
  pitchCount: number,
  settings: TournamentSettings,
  bounds: TournamentBounds,
) {
  if (!day.date || !day.startTime || pitchCount === 0) return 0
  const slot = settings.matchDurationMinutes + settings.breakDurationMinutes
  const free = dayLimit(day.date, bounds) - toMinutes(day.startTime) - settings.matchDurationMinutes
  return free < 0 ? 0 : (Math.floor(free / slot) + 1) * pitchCount
}

/** Szacowany koniec ostatniego meczu dnia — mecze idą równolegle na wszystkich wybranych boiskach. */
function dayEnd(day: DayValues, pitchCount: number, settings: TournamentSettings) {
  if (!day.startTime || !pitchCount || !(day.matches > 0)) return null
  const slot = settings.matchDurationMinutes + settings.breakDurationMinutes
  const slots = Math.ceil(day.matches / pitchCount)
  return toMinutes(day.startTime) + (slots - 1) * slot + settings.matchDurationMinutes
}

type GroupPlan = { group: TournamentGroup; teams: number; matches: number; rounds: number }

/** Co generator rozłoży w czasie: mecze grupowe albo (puchar) mecze 1. rundy z drabinki. */
type Workload = {
  totalMatches: number
  maxRounds: number
  /** Grupy + puchar: ile z totalMatches to mecze pucharu (dostaną termin po fazie grupowej). */
  knockoutMatches?: number
  /** Puchar: etap każdego meczu w kolejności, w jakiej generator rozdaje je dniom. */
  stages?: string[]
}

/**
 * Wszystkie mecze pucharu, które dostaną termin: najpierw te, które już istnieją (1. runda), potem
 * kolejne rundy czekające na zwycięzców — w tej samej kolejności co generator w backendzie.
 * Dzięki temu "ile meczów którego dnia" przekłada się wprost na "która runda którego dnia".
 */
function bracketSequence(brackets: Bracket[], pending: Match[]) {
  const pendingIds = new Set(pending.map((m) => m.id))
  const items: { phase: number; order: number; title: string }[] = []
  for (const bracket of brackets) {
    const stageByRound = new Map(bracketStages(bracket).map((stage) => [stage.roundId, stage]))
    for (const round of bracket.rounds) {
      const stage = stageByRound.get(round.id)
      if (!stage) continue
      for (const node of round.nodes) {
        if (node.matchId) {
          if (pendingIds.has(node.matchId))
            items.push({ phase: 0, order: round.roundNumber, title: stage.title })
        } else if (node.homeSourceType !== "Bye" && node.awaySourceType !== "Bye") {
          // Węzeł z wolnym losem nigdy nie dostaje meczu.
          items.push({ phase: 1, order: stage.order, title: stage.title })
        }
      }
    }
  }
  return items.sort((a, b) => a.phase - b.phase || a.order - b.order).map((item) => item.title)
}

/**
 * Ile meczów będzie miała drabinka z tych par (1. runda bez wolnych losów, kolejne rundy i mecz
 * o 3. miejsce). Generator planuje je od razu, więc liczą się do meczów dnia.
 */
function bracketMatchCount(
  pairs: { home: { kind: string }; away: { kind: string } }[],
  thirdPlace: boolean,
) {
  const byes = pairs.filter((p) => p.home.kind === "bye" || p.away.kind === "bye").length
  return pairs.length - byes + (pairs.length - 1) + (thirdPlace && pairs.length >= 2 ? 1 : 0)
}

/** "Półfinały × 2, Finał" — etapy meczów jednego dnia. */
function summarizeStages(titles: string[]) {
  const groups: { title: string; count: number }[] = []
  for (const title of titles) {
    const last = groups.at(-1)
    if (last?.title === title) last.count++
    else groups.push({ title, count: 1 })
  }
  return groups
    .map(({ title, count }) =>
      count === 1 && (title === "Finał" || title.startsWith("Mecz"))
        ? title
        : `${title} × ${count}`,
    )
    .join(", ")
}

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

/**
 * Te same reguły co GenerateScheduleCommandHandler: grupa gra, jeśli ma ≥ 2 zaakceptowane drużyny.
 * Pusta grupa nie przeszkadza, grupa z jedną drużyną blokuje generowanie (ta drużyna nie miałaby meczu).
 */
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

function Requirement({
  ok,
  warn = false,
  children,
}: {
  ok: boolean
  /** Nie blokuje generowania, ale warto to sprawdzić przed nim. */
  warn?: boolean
  children: ReactNode
}) {
  return (
    <li className="flex items-start gap-2 text-sm">
      {warn ? (
        <TriangleAlert className="text-live mt-0.5 size-4 shrink-0" aria-label="Uwaga" />
      ) : ok ? (
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

/**
 * Zaakceptowane drużyny bez grupy nie dostają żadnego meczu. Gdy kategoria ma już grupy, rozdzielamy
 * je jednym kliknięciem — każda trafia do grupy, w której jest aktualnie najmniej drużyn.
 */
function DistributeTeams({
  tournamentId,
  categoryName,
  groups,
  unassigned,
  allTeams,
}: {
  tournamentId: string
  categoryName: string | null
  groups: TournamentGroup[]
  unassigned: TournamentTeam[]
  allTeams: TournamentTeam[]
}) {
  const assign = useAssignTeamToGroup(tournamentId)
  const [pending, setPending] = useState(false)

  async function run() {
    setPending(true)
    const sizes = new Map(
      groups.map((g) => [
        g.id,
        allTeams.filter((t) => t.groupId === g.id && t.registrationStatus === "Accepted").length,
      ]),
    )
    try {
      for (const team of unassigned) {
        const target = groups.reduce((a, b) => (sizes.get(b.id)! < sizes.get(a.id)! ? b : a))
        await assign.mutateAsync({ tournamentTeamId: team.id, groupId: target.id })
        sizes.set(target.id, sizes.get(target.id)! + 1)
      }
      toast.success(`Przypisano ${plural(unassigned.length, ["drużynę", "drużyny", "drużyn"])}`)
    } finally {
      setPending(false)
    }
  }

  const where =
    groups.length === 1
      ? `do „${groups[0]!.name}”`
      : `po równo między ${plural(groups.length, ["grupę", "grupy", "grup"])}`
  return (
    <Button
      type="button"
      size="sm"
      variant="outline"
      className="mt-2 mr-2"
      onClick={run}
      disabled={pending}
    >
      {pending
        ? "Przypisywanie…"
        : `${groups.length === 1 ? "Przypisz" : "Rozdziel"} ${plural(unassigned.length, ["drużynę", "drużyny", "drużyn"])}${categoryName ? ` (${categoryName})` : ""} ${where}`}
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
  manualBrackets,
  lonelyCategories,
}: {
  tournament: Tournament
  plan: GroupPlan[]
  bracketMatches: number
  activePitches: number
  teams: TournamentTeam[]
  groups: TournamentGroup[]
  categories: TournamentCategory[]
  /** Kategorie, w których drabinkę musi ułożyć organizator. */
  manualBrackets: TournamentCategory[]
  /** Kategorie z jedną zaakceptowaną drużyną. */
  lonelyCategories: TournamentCategory[]
}) {
  const id = tournament.id
  const playable = plan.filter((p) => p.matches > 0)
  const incomplete = plan.filter((p) => p.teams === 0)
  const singleTeam = plan.filter((p) => p.teams === 1)
  // Diagnoza "dlaczego grupa nie gra": drużyny bez grupy i czekające na akceptację.
  const accepted = teams.filter((t) => t.registrationStatus === "Accepted")
  const unassigned = accepted.filter((t) => !t.groupId)
  const waiting = teams.filter((t) => t.registrationStatus === "Pending")
  // Kategorie bez żadnej grupy, w których są co najmniej 2 zaakceptowane drużyny bez grupy.
  const quickSetup = categories
    .filter((c) => !groups.some((g) => g.categoryId === c.id))
    .map((c) => ({ category: c, teams: unassigned.filter((t) => t.categoryId === c.id) }))
    .filter((x) => x.teams.length >= 2)
  // Kategorie, które mają grupy, ale część zaakceptowanych drużyn do żadnej nie trafiła (przy kilku
  // grupach przypisania nie ma automatycznie).
  const toDistribute = categories
    .map((c) => ({
      category: c,
      groups: groups.filter((g) => g.categoryId === c.id).sort((a, b) => a.order - b.order),
      teams: unassigned.filter((t) => t.categoryId === c.id),
    }))
    .filter((x) => x.groups.length > 0 && x.teams.length > 0)
  return (
    <ul className="space-y-2">
      <Requirement ok={tournament.status === "RegistrationClosed"}>
        {tournament.status === "RegistrationClosed"
          ? "Zapisy zamknięte"
          : tournament.status === "Draft"
            ? "Zapisy jeszcze nie ruszyły"
            : "Zapisy są wciąż otwarte"}
        {tournament.status !== "RegistrationClosed" && (
          <span className="text-muted-foreground block text-xs">
            {tournament.status === "Draft"
              ? "Terminarz układa się dla ostatecznej listy drużyn. Otwórz zapisy, a gdy wszystkie drużyny się zgłoszą, zamknij je."
              : "Terminarz układa się dla ostatecznej listy drużyn. Zamknij zapisy, gdy wszystkie drużyny się zgłoszą."}
          </span>
        )}
        <RegistrationAction tournament={tournament} />
      </Requirement>
      <Requirement ok={!!tournament.format}>
        {tournament.format
          ? `Format: ${TOURNAMENT_FORMAT_LABELS[tournament.format]}`
          : "Format turnieju - wybierz, jak grają drużyny:"}
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
                - drabinka nie ma z kogo ułożyć par.{" "}
                <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
                  Przejdź do Drużyn
                </Link>
              </span>
            )}
          </Requirement>
          <Requirement ok={bracketMatches > 0}>
            {bracketMatches > 0 ? (
              <>
                Drabinka gotowa: {plural(bracketMatches, ["mecz", "mecze", "meczów"])} 1. rundy do
                rozstawienia{" "}
                <Link href={`/tournaments/${id}/bracket`} className="text-primary underline">
                  (zobacz pary)
                </Link>
              </>
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
              Pominięte (bez drużyn): {incomplete.map((p) => p.group.name).join(", ")}
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
              Turniej nie ma jeszcze żadnej grupy - mecze grają drużyny przypisane do tej samej
              grupy.{" "}
              <Link href={`/tournaments/${id}/categories`} className="text-primary underline">
                Dodaj grupę
              </Link>
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
      {singleTeam.length > 0 && (
        <Requirement ok={false}>
          Tylko jedna drużyna w grupie: {singleTeam.map((p) => p.group.name).join(", ")}
          <span className="text-muted-foreground block text-xs">
            Ta drużyna nie miałaby meczu, dodaj jej rywala albo przenieś ją do innej grupy.{" "}
            <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
              Przejdź do Drużyn
            </Link>
          </span>
        </Requirement>
      )}
      {lonelyCategories.length > 0 && (
        <Requirement ok={false}>
          Tylko jedna drużyna w kategorii: {lonelyCategories.map((c) => c.name).join(", ")}
          <span className="text-muted-foreground block text-xs">
            Ta drużyna nie miałaby z kim grać, dodaj drugą albo usuń kategorię.{" "}
            <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
              Przejdź do Drużyn
            </Link>
          </span>
        </Requirement>
      )}
      {manualBrackets.length > 0 && (!isKnockout(tournament) || categories.length > 1) && (
        <Requirement ok={false}>
          Brak drabinki
          {categories.length > 1 && `: ${manualBrackets.map((c) => c.name).join(", ")}`}{" "}
          <Link href={`/tournaments/${id}/bracket`} className="text-primary underline">
            (ułóż pary)
          </Link>
          <span className="text-muted-foreground block text-xs">
            {isKnockout(tournament)
              ? "Terminarz powstaje dla wszystkich kategorii naraz, więc każda potrzebuje drabinki."
              : "Pary z miejsc w grupach nie ułożyły się same, a terminarz od razu planuje mecze pucharu."}
          </span>
        </Requirement>
      )}
      {!isKnockout(tournament) && unassigned.length > 0 && (
        <Requirement ok={false}>
          {plural(unassigned.length, [
            "zaakceptowana drużyna nie ma",
            "zaakceptowane drużyny nie mają",
            "zaakceptowanych drużyn nie ma",
          ])}{" "}
          grupy: {unassigned.map((t) => t.teamName).join(", ")}
          <span className="text-muted-foreground block text-xs">
            Mecze grają tylko drużyny przypisane do grupy - te nie dostałyby żadnego, a terminarz
            generuje się raz.{" "}
            <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
              Przypisz ręcznie w Drużynach
            </Link>
          </span>
          {toDistribute.map(({ category, groups: categoryGroups, teams: categoryTeams }) => (
            <DistributeTeams
              key={category.id}
              tournamentId={id}
              categoryName={categories.length > 1 ? category.name : null}
              groups={categoryGroups}
              unassigned={categoryTeams}
              allTeams={teams}
            />
          ))}
        </Requirement>
      )}
      {waiting.length > 0 && (
        <Requirement ok={false} warn>
          {plural(waiting.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])} na
          akceptację: {waiting.map((t) => t.teamName).join(", ")}
          <span className="text-muted-foreground block text-xs">
            Nie dostaną meczów, a terminarz generuje się raz. Zaakceptuj je przed generowaniem albo
            odrzuć, jeśli nie zagrają.{" "}
            <Link href={`/tournaments/${id}/teams`} className="text-primary underline">
              Przejdź do Drużyn
            </Link>
          </span>
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
  acceptedTeams,
  waitingTeams,
  notReadyReason,
  beforeGenerate,
}: {
  tournament: Tournament
  settings: TournamentSettings
  pitches: Pitch[]
  workload: Workload
  ready: boolean
  acceptedTeams: number
  /** Nazwy drużyn czekających na akceptację: nie dostaną meczów. */
  waitingTeams: string[]
  /** Dlaczego przycisk jest wyłączony, gdy warunki nie są spełnione. */
  notReadyReason: string
  /**
   * Grupy + puchar: przed terminarzem tworzymy brakujące drabinki (miejsca w grupach), bo generator
   * od razu planuje terminy pucharu. false = nie wszystkie powstały, nie generujemy.
   */
  beforeGenerate?: () => Promise<boolean>
}) {
  const router = useRouter()
  const generate = useGenerateSchedule(tournament.id)
  const { totalMatches, stages, knockoutMatches = 0 } = workload
  const [preparing, setPreparing] = useState(false)
  // Szkic przeżywa przejście do innej sekcji (np. "zmień" czas meczu w ustawieniach) i powrót.
  const [draft] = useState(() => readDraft(tournament.id))
  const [bounds] = useState((): TournamentBounds => {
    const start = toDateTimeLocal(tournament.startAt)
    const end = toDateTimeLocal(tournament.endAt)
    return {
      startDate: start.slice(0, 10),
      startTime: start.slice(11),
      endDate: end.slice(0, 10),
      endTime: end.slice(11),
    }
  })
  const form = useForm<GenerateValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      // Start: jeden dzień — pierwszy dzień turnieju — ze wszystkimi meczami. Kolejne dni dodaje
      // organizator. Szkic wygrywa, o ile jego dni nadal mieszczą się w datach turnieju.
      days: draft?.days.every((day) => day.date >= bounds.startDate && day.date <= bounds.endDate)
        ? draft.days
        : [
            {
              date: bounds.startDate,
              startTime: bounds.startTime,
              matches: Math.max(totalMatches, 1),
            },
          ],
      pitchIds: draft
        ? draft.pitchIds.filter((id) => pitches.some((p) => p.id === id))
        : pitches.map((p) => p.id),
    },
  })
  const dayFields = useFieldArray({ control: form.control, name: "days" })
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
  // Jeden dzień gra wszystkie mecze — liczby nie ma czego wpisywać (pole jest ukryte). Dotyczy też
  // szkicu sprzed zmiany liczby meczów i powrotu do jednego dnia po usunięciu pozostałych.
  const singleDay = days.length === 1
  useEffect(() => {
    if (!singleDay) return
    const all = Math.max(totalMatches, 1)
    if (form.getValues("days.0.matches") !== all)
      form.setValue("days.0.matches", all, { shouldValidate: true })
  }, [singleDay, totalMatches, form])

  const pitchCount = pitchIds.length
  const count = (day: DayValues) => (Number.isFinite(day.matches) ? day.matches : 0)
  const assigned = days.reduce((sum, day) => sum + count(day), 0)
  const remaining = totalMatches - assigned
  const sorted = [...days].sort((a, b) => a.date.localeCompare(b.date))
  const lastDay = sorted.at(-1)
  const lastEnd = lastDay ? dayEnd(lastDay, pitchCount, settings) : null
  // Dzień po ostatnim z listy — o ile turniej jeszcze wtedy trwa.
  const dateToAdd = lastDay?.date ? nextDate(lastDay.date) : bounds.startDate
  const canAddDay = dateToAdd <= bounds.endDate
  // Ile meczów w ogóle mieści się w wpisanych dniach. Gdy mniej niż trzeba, a dnia nie da się
  // już dodać, podpowiedź „zwiększ liczbę meczów” nic nie da — brakuje czasu.
  const fitTotal = days.reduce(
    (sum, day) => sum + dayCapacity(day, pitchCount, settings, bounds),
    0,
  )
  const outOfTime = remaining > 0 && pitchCount > 0 && !canAddDay && fitTotal < totalMatches

  // Puchar: które rundy wypadają którego dnia — mecze rozdajemy dniom po kolei, według dat.
  const stagesOfDay: (string | null)[] = days.map(() => null)
  if (stages) {
    let offset = 0
    for (const index of days
      .map((_, i) => i)
      .sort((a, b) => days[a]!.date.localeCompare(days[b]!.date))) {
      const slice = stages.slice(offset, offset + count(days[index]!))
      offset += count(days[index]!)
      stagesOfDay[index] = slice.length ? summarizeStages(slice) : null
    }
  }

  // Daty turnieju zmienia się w ustawieniach; szkic formularza zostaje w sessionStorage.
  const datesLink = (
    <Link
      href={`/tournaments/${tournament.id}/settings#details`}
      className="text-foreground ml-1 underline"
    >
      Zmień daty turnieju
    </Link>
  )

  /** Co jest nie tak z danym dniem (poza samymi polami) — blokuje generowanie. */
  function dayProblem(day: DayValues, index: number): ReactNode {
    if (!day.date) return null
    if (day.date < bounds.startDate || day.date > bounds.endDate)
      return (
        <>
          Turniej trwa {formatDateTime(tournament.startAt)} – {formatDateTime(tournament.endAt)}.
          {datesLink}
        </>
      )
    if (days.findIndex((other) => other.date === day.date) !== index)
      return "Ten dzień jest już na liście"
    if (day.date === bounds.startDate && day.startTime && day.startTime < bounds.startTime)
      return `Turniej zaczyna się o ${bounds.startTime}`
    const capacity = dayCapacity(day, pitchCount, settings, bounds)
    if (pitchCount > 0 && count(day) > capacity)
      return capacity === 0 ? (
        "Tego dnia nie zmieści się już żaden mecz - wybierz wcześniejszą godzinę"
      ) : day.date === bounds.endDate ? (
        <>
          Tego dnia zmieści się najwyżej {plural(capacity, ["mecz", "mecze", "meczów"])}, bo turniej
          kończy się o {bounds.endTime}.{datesLink}
        </>
      ) : (
        `Tego dnia zmieści się najwyżej ${plural(capacity, ["mecz", "mecze", "meczów"])}`
      )
    return null
  }
  const problems = days.map(dayProblem)
  const planOk = remaining === 0 && problems.every((problem) => !problem)

  /** Po równo na wszystkie dni z listy; reszta z dzielenia trafia do pierwszych dni. */
  function splitEvenly(dayCount = days.length) {
    if (dayCount === 0 || totalMatches === 0) return
    const base = Math.floor(totalMatches / dayCount)
    const extra = totalMatches % dayCount
    for (let index = 0; index < dayCount; index++) {
      form.setValue(`days.${index}.matches`, Math.max(base + (index < extra ? 1 : 0), 1), {
        shouldDirty: true,
        shouldValidate: true,
      })
    }
  }

  function addDay() {
    dayFields.append({
      date: dateToAdd,
      startTime: lastDay?.startTime || bounds.startTime,
      // Nowy dzień bierze to, co jeszcze nie ma terminu. Gdy wszystko już rozdane, dzielimy od nowa.
      matches: Math.max(remaining, 1),
    })
    if (remaining <= 0) splitEvenly(days.length + 1)
  }

  // Generowanie jest jednorazowe, więc przed wysłaniem pytamy jeszcze raz.
  const [confirming, setConfirming] = useState<GenerateValues | null>(null)

  async function submit(values: GenerateValues) {
    if (beforeGenerate) {
      setPreparing(true)
      const prepared = await beforeGenerate().finally(() => setPreparing(false))
      if (!prepared) return
    }
    const windows = [...values.days]
      .sort((a, b) => a.date.localeCompare(b.date))
      .map((day) => ({
        startAt: zonedToIso(day.date, day.startTime),
        // Okno jest szerokie (do końca dnia / turnieju) — o liczbie meczów decyduje maxMatches,
        // więc generator ma zapas na przerwy drużyn i nie odbija się od "za mało czasu".
        endAt:
          day.date === bounds.endDate
            ? new Date(tournament.endAt).toISOString()
            : zonedToIso(day.date, "23:59"),
        maxMatches: day.matches,
      }))
    generate.mutate(
      {
        startAt: windows[0]!.startAt,
        endAt: windows.at(-1)!.endAt,
        windows,
        pitchIds: values.pitchIds,
      },
      {
        onSuccess: async ({ matchesCreated }) => {
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
    <form onSubmit={form.handleSubmit(setConfirming)} noValidate>
      <AlertDialog open={!!confirming} onOpenChange={(open) => !open && setConfirming(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Wygenerować terminarz?</AlertDialogTitle>
            <AlertDialogDescription>
              {isKnockout(tournament)
                ? `${plural(totalMatches, ["mecz 1. rundy dostanie", "mecze 1. rundy dostaną", "meczów 1. rundy dostanie"])} godziny i boiska.`
                : `Powstanie ${plural(totalMatches - knockoutMatches, ["mecz", "mecze", "meczów"])} dla ${plural(acceptedTeams, ["zaakceptowanej drużyny", "zaakceptowanych drużyn", "zaakceptowanych drużyn"])}${knockoutMatches > 0 ? `, a ${plural(knockoutMatches, ["mecz pucharu dostanie", "mecze pucharu dostaną", "meczów pucharu dostanie"])} godziny` : ""}.`}{" "}
              Turniej przejdzie do etapu „Harmonogram gotowy”. Żeby ułożyć terminarz jeszcze raz,
              trzeba go najpierw wyczyścić.
            </AlertDialogDescription>
          </AlertDialogHeader>
          {waitingTeams.length > 0 && (
            <div
              role="alert"
              className="border-live/50 bg-live/10 rounded-xl border px-4 py-3 text-sm"
            >
              <p className="font-medium">
                {plural(waitingTeams.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])}{" "}
                na akceptację i nie dostanie meczów
              </p>
              <p className="text-muted-foreground">{waitingTeams.join(", ")}</p>
            </div>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel>Anuluj</AlertDialogCancel>
            {waitingTeams.length > 0 && (
              <Button asChild variant="outline">
                <Link href={`/tournaments/${tournament.id}/teams`}>Przejdź do Drużyn</Link>
              </Button>
            )}
            <AlertDialogAction
              onClick={() => {
                if (confirming) void submit(confirming)
                setConfirming(null)
              }}
            >
              Generuj terminarz
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <FieldGroup>
        <p className="bg-muted/40 rounded-lg border px-3 py-2 text-sm">
          <span className="font-medium">{plural(totalMatches, ["mecz", "mecze", "meczów"])}</span>{" "}
          do rozplanowania · mecz{" "}
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

        <FieldSet>
          <FieldLegend variant="label">Dni gry</FieldLegend>
          <FieldDescription>
            {singleDay
              ? "Podaj dzień i godzinę pierwszego meczu. Jeśli gracie kilka dni, dodaj kolejny dzień i rozdziel mecze."
              : "Dla każdego dnia podaj, od której godziny i ile meczów gracie. Dni, których nie dodasz (np. weekend), zostają bez meczów."}{" "}
            Ostatni mecz musi się skończyć do {formatDateTime(tournament.endAt)}.
          </FieldDescription>
          <ul className="space-y-3">
            {dayFields.fields.map((field, index) => {
              const day = days[index]
              const end = day ? dayEnd(day, pitchCount, settings) : null
              const dayErrors = errors.days?.[index]
              const problem = problems[index]
              return (
                <li key={field.id} className="rounded-lg border p-3">
                  <div
                    className={cn(
                      "grid grid-cols-2 items-start gap-3",
                      singleDay
                        ? "sm:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]"
                        : "sm:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)_minmax(0,0.8fr)_auto]",
                    )}
                  >
                    <div className="col-span-2 sm:col-span-1">
                      <TextField
                        id={`schedule-date-${index}`}
                        label={
                          day?.date
                            ? `Dzień - ${formatDayLong(`${day.date}T12:00:00Z`).split(",")[0]}`
                            : "Dzień"
                        }
                        type="date"
                        min={bounds.startDate}
                        max={bounds.endDate}
                        className="font-mono tabular-nums"
                        error={dayErrors?.date}
                        {...form.register(`days.${index}.date`)}
                      />
                    </div>
                    <TextField
                      id={`schedule-start-${index}`}
                      label="Pierwszy mecz"
                      type="time"
                      className="font-mono tabular-nums"
                      error={dayErrors?.startTime}
                      {...form.register(`days.${index}.startTime`)}
                    />
                    {!singleDay && (
                      <>
                        <TextField
                          id={`schedule-matches-${index}`}
                          label="Meczów"
                          type="number"
                          inputMode="numeric"
                          min={1}
                          className="font-mono tabular-nums"
                          error={dayErrors?.matches}
                          {...form.register(`days.${index}.matches`, { valueAsNumber: true })}
                        />
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="text-muted-foreground col-span-2 justify-self-end sm:col-span-1 sm:mt-6"
                          aria-label={`Usuń dzień ${index + 1}`}
                          title="Usuń dzień"
                          onClick={() => dayFields.remove(index)}
                        >
                          <X />
                        </Button>
                      </>
                    )}
                  </div>
                  {problem ? (
                    <p className="text-destructive mt-2 text-sm">{problem}</p>
                  ) : (
                    end !== null &&
                    day && (
                      <p className="text-muted-foreground mt-2 text-xs">
                        Mecze{" "}
                        <span className="font-mono tabular-nums">
                          {day.startTime} – ok. {toTime(Math.min(end, 24 * 60 - 1))}
                        </span>
                        {pitchCount > 1 &&
                          ` na ${plural(pitchCount, ["boisku", "boiskach", "boiskach"])} równolegle`}
                        {stagesOfDay[index] && (
                          <span className="text-foreground"> · {stagesOfDay[index]}</span>
                        )}
                      </p>
                    )
                  )}
                </li>
              )
            })}
          </ul>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={addDay}
              disabled={!canAddDay}
            >
              <Plus />
              Dodaj dzień
            </Button>
            {days.length > 1 && totalMatches > 0 && (
              <Button type="button" size="sm" variant="ghost" onClick={() => splitEvenly()}>
                Rozłóż mecze równo
              </Button>
            )}
            {!canAddDay && (
              <span className="text-muted-foreground text-xs">
                Turniej kończy się {formatDateTime(tournament.endAt)}, więc nie ma kolejnego dnia.
                {datesLink}
              </span>
            )}
          </div>
          <FieldError errors={[errors.days?.root]} />
          <p className="text-muted-foreground flex items-start gap-2 text-xs">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              Godzina końca dnia to szacunek. Drużyna nie gra dwóch meczów naraz, więc przy kilku
              boiskach dzień może skończyć się trochę później, niż pokazuje formularz.
              {isKnockout(tournament) &&
                " Mecze trafiają do dni w kolejności rund - żeby np. finał wypadł innego dnia, zostaw na ten dzień tylko tyle meczów, ile ma się w nim odbyć."}
            </span>
          </p>
        </FieldSet>

        {totalMatches > 0 && (
          <div
            role="status"
            className={cn(
              "rounded-lg border px-3 py-2.5 text-sm",
              planOk ? "border-primary/40 bg-primary/5" : "border-live/50 bg-live/10",
            )}
          >
            {outOfTime ? (
              <>
                <p className="font-medium">
                  Brakuje czasu na {plural(totalMatches - fitTotal, ["mecz", "mecze", "meczów"])}.
                </p>
                <p className="text-muted-foreground">
                  Do końca turnieju ({formatDateTime(tournament.endAt)}) zmieści się {fitTotal} z{" "}
                  {totalMatches}. Przesuń koniec turnieju w{" "}
                  <Link
                    href={`/tournaments/${tournament.id}/settings#details`}
                    className="text-primary underline"
                  >
                    ustawieniach
                  </Link>
                  , skróć mecz lub przerwę
                  {pitchCount < pitches.length ? ", zaznacz więcej boisk" : ""} albo zacznij
                  wcześniej.
                </p>
              </>
            ) : remaining > 0 ? (
              <>
                <p className="font-medium">
                  {plural(remaining, [
                    "mecz nie ma jeszcze dnia",
                    "mecze nie mają jeszcze dnia",
                    "meczów nie ma jeszcze dnia",
                  ])}
                  .
                </p>
                <p className="text-muted-foreground">
                  Rozplanowano {assigned} z {totalMatches}. Zwiększ liczbę meczów któregoś dnia
                  {canAddDay ? " albo dodaj kolejny dzień" : ""}.
                </p>
              </>
            ) : remaining < 0 ? (
              <>
                <p className="font-medium">
                  Dni mają o {plural(-remaining, ["mecz", "mecze", "meczów"])} za dużo.
                </p>
                <p className="text-muted-foreground">
                  Do rozegrania jest {totalMatches}, a w dniach wpisano {assigned}. Zmniejsz liczbę
                  meczów albo użyj „Rozłóż mecze równo”.
                </p>
              </>
            ) : !planOk ? (
              <p className="font-medium">Popraw dni oznaczone na czerwono.</p>
            ) : (
              <>
                <p className="font-medium">
                  Wszystkie {plural(totalMatches, ["mecz", "mecze", "meczów"])}{" "}
                  {totalMatches === 1 ? "ma" : "mają"} swój dzień
                  {days.length > 1 && ` - ${plural(days.length, ["dzień", "dni", "dni"])} gry`}.
                </p>
                {lastDay && lastEnd !== null && (
                  <p className="text-muted-foreground">
                    Ostatni mecz skończy się ok.{" "}
                    <span className="font-mono tabular-nums">
                      {formatDayLong(`${lastDay.date}T12:00:00Z`)}, {toTime(lastEnd)}
                    </span>
                    .
                  </p>
                )}
              </>
            )}
          </div>
        )}
        <FieldDescription className="-mt-4">
          Godziny w czasie polskim. Każdy mecz możesz potem przesunąć w terminarzu.
        </FieldDescription>

        <FieldError errors={[errors.root]} />

        {!ready && (
          <p
            role="status"
            className="border-live/50 bg-live/10 rounded-xl border px-4 py-3 text-sm"
          >
            {notReadyReason}
          </p>
        )}

        <div className="flex flex-wrap gap-2">
          <Button type="submit" disabled={!ready || !planOk || generate.isPending || preparing}>
            <CalendarCheck />
            {generate.isPending || preparing ? "Generowanie…" : "Generuj terminarz"}
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
  const knockout = tournament.data?.format === "Knockout"
  const withBracket = knockout || tournament.data?.format === "GroupAndKnockout"
  const brackets = useBrackets(
    id,
    (categories.data ?? []).map((c) => c.id),
    withBracket,
  )
  const { mutateAsync: createBracket, isPending: creatingBracket } = useGenerateBracket(id)
  const acceptedIn = (categoryId: string) =>
    (teams.data ?? []).filter(
      (team) => team.categoryId === categoryId && team.registrationStatus === "Accepted",
    )
  // Kategorie bez drabinki (GET zwraca 404), w których ktoś gra — drabinka powstaje sama z propozycji
  // par. Każda z nich musi ją mieć, inaczej generator odmówi (dawniej ta kategoria zostawała bez meczów).
  const missingBrackets = (categories.data ?? []).filter((category, index) => {
    const bracket = brackets[index]
    return (
      acceptedIn(category.id).length >= 2 &&
      !!bracket?.isError &&
      isApiError(bracket.error) &&
      bracket.error.status === 404
    )
  })

  // Puchar: terminarz rozstawia mecze 1. rundy, więc drabinka musi powstać przed generowaniem.
  // Tworzymy ją po wejściu tutaj (zapisy zamknięte, drużyny ustalone), każdą kategorię raz.
  const autoKnockout =
    isOrganizer && knockout && tournament.data?.status === "RegistrationClosed" && !!teams.data
  const missingKey = missingBrackets.map((c) => c.id).join(",")
  const presentKey = (categories.data ?? [])
    .filter((_, index) => !!brackets[index]?.data)
    .map((c) => c.id)
    .join(",")
  // Kategoria, która ma drabinkę, może ją znów stracić (organizator ją usunie, żeby ułożyć pary od nowa).
  // Wtedy drabinka ma powstać sama ponownie, więc zapominamy, że już próbowaliśmy.
  useEffect(() => {
    for (const categoryId of presentKey ? presentKey.split(",") : [])
      autoBracketTried.delete(`${id}:${categoryId}`)
  }, [presentKey, id])
  useEffect(() => {
    if (!autoKnockout || !missingKey) return
    const todo = missingKey
      .split(",")
      .filter((categoryId) => !autoBracketTried.has(`${id}:${categoryId}`))
    todo.forEach((categoryId) => autoBracketTried.add(`${id}:${categoryId}`))
    // Po kolei, nie równolegle: backend daje meczom 1. rundy pierwszy wolny termin na boisku, więc dwie
    // drabinki tworzone naraz brały ten sam termin, druga padała w połowie ("na tym boisku trwa inny
    // mecz") i zostawała bez meczów.
    void (async () => {
      for (const categoryId of todo) {
        const accepted = (teams.data ?? []).filter(
          (team) => team.categoryId === categoryId && team.registrationStatus === "Accepted",
        )
        const pairs = autoBracketPairs([], accepted)
        if (!pairs) continue
        try {
          await createBracket({ categoryId, pairs })
        } catch {
          // Błąd pokazuje globalny handler; po powrocie na ekran spróbujemy jeszcze raz.
          autoBracketTried.delete(`${id}:${categoryId}`)
        }
      }
    })()
  }, [autoKnockout, missingKey, teams.data, createBracket, id])

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
    categories.isPending ||
    // Kategoria bez drabinki kończy się błędem 404, nie wiecznym ładowaniem. Tylko pierwsze ładowanie:
    // po utworzeniu drabinki zapytanie wraca na chwilę do "pending", a szkielet w tym czasie
    // przemontowywał formularz w trakcie generowania (ginęło przejście do terminarza).
    (withBracket && brackets.some((bracket) => bracket.isPending && !bracket.isFetched)) ||
    // Grupy + puchar tworzą drabinki w trakcie generowania - formularz musi wtedy zostać na ekranie.
    (knockout && creatingBracket)
  if (loading) return <Skeleton className="h-96 w-full max-w-3xl rounded-xl" />

  const t = tournament.data!
  const activePitches = (pitches.data ?? []).filter((p) => p.isActive)
  const plan = planGroups(groups.data ?? [], teams.data ?? [])
  const pendingBracket = pendingBracketMatches(schedule.data ?? [])
  const bracketMatches = pendingBracket.length
  // Puchar: rozplanowujemy całą drabinkę — mecze 1. rundy i kolejne rundy, które jeszcze nie powstały.
  const stages = bracketSequence(
    brackets.flatMap((bracket) => (bracket.data ? [bracket.data] : [])),
    pendingBracket,
  )
  // Grupy + puchar: propozycja drabinki z miejsc w grupach dla kategorii, które jej jeszcze nie mają.
  // Kategoria bez propozycji (np. pary trzeba poprawić) czeka na organizatora w zakładce Drabinka.
  const groupBracketProposals =
    t.format === "GroupAndKnockout"
      ? missingBrackets.map((category) => {
          const categoryGroups = plan
            .filter((p) => p.group.categoryId === category.id && p.matches > 0)
            .map((p) => p.group)
            .sort((a, b) => a.order - b.order)
          return {
            category,
            pairs:
              categoryGroups.length > 0
                ? autoBracketPairs(categoryGroups, acceptedIn(category.id))
                : null,
          }
        })
      : []
  const groupMatches = plan.reduce((sum, p) => sum + p.matches, 0)
  const knockoutMatches =
    t.format === "GroupAndKnockout"
      ? stages.length +
        groupBracketProposals.reduce(
          (sum, { pairs }) =>
            sum + (pairs ? bracketMatchCount(pairs, !!settings.data?.isThirdPlaceMatchEnabled) : 0),
          0,
        )
      : 0
  const workload: Workload = isKnockout(t)
    ? {
        totalMatches: bracketMatches > 0 ? Math.max(stages.length, bracketMatches) : 0,
        maxRounds: bracketMatches > 0 ? 1 : 0,
        // Etapy pokazujemy tylko, gdy lista zgadza się z meczami z terminarza.
        stages: stages.length >= bracketMatches ? stages : undefined,
      }
    : {
        // Grupy + puchar: generator od razu daje termin także meczom pucharu, więc liczą się do dni.
        totalMatches: groupMatches + knockoutMatches,
        maxRounds: Math.max(0, ...plan.map((p) => p.rounds)),
        knockoutMatches,
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

  // Grupy + puchar: przed generowaniem drabinka z miejsc w grupach (A1–B2, B1–A2) w każdej kategorii,
  // która jej nie ma. Generator od razu planuje jej mecze po fazie grupowej tej kategorii.
  async function createGroupBrackets() {
    let created = 0
    for (const { category, pairs } of groupBracketProposals) {
      if (!pairs) return false
      try {
        await createBracket({ categoryId: category.id, pairs })
        created++
      } catch {
        // Błąd pokazuje globalny handler; terminarza bez drabinki tej kategorii nie generujemy.
        return false
      }
    }
    if (created > 0)
      toast.success("Drabinka utworzona z miejsc w grupach. Pary zmienisz w zakładce Drabinka.")
    return true
  }

  // Kategorie, w których drabinkę musi ułożyć organizator: puchar, gdy nie powstała sama, i grupy +
  // puchar, gdy propozycji z miejsc w grupach nie da się zapisać bez poprawek.
  const manualBrackets = isKnockout(t)
    ? missingBrackets
    : groupBracketProposals.filter((p) => !p.pairs).map((p) => p.category)
  // Kategoria z jedną drużyną nie ma z kim grać, a terminarz powstaje dla całego turnieju.
  const lonelyCategories = (categories.data ?? []).filter((c) => acceptedIn(c.id).length === 1)

  // Zaakceptowana drużyna bez grupy zostałaby po cichu pominięta — a generowanie jest jednorazowe.
  const teamsWithoutGroup = (teams.data ?? []).some(
    (team) => team.registrationStatus === "Accepted" && !team.groupId,
  )
  const registrationClosed = t.status === "RegistrationClosed"
  const ready =
    registrationClosed &&
    (isKnockout(t)
      ? bracketMatches > 0
      : hasGroupStage(t) &&
        plan.some((p) => p.matches > 0) &&
        !plan.some((p) => p.teams === 1) &&
        !teamsWithoutGroup) &&
    manualBrackets.length === 0 &&
    lonelyCategories.length === 0 &&
    activePitches.length > 0

  return (
    <div className="max-w-3xl space-y-4">
      {backLink}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Warunki</CardTitle>
          <CardDescription>
            {isKnockout(t)
              ? "Puchar: pary 1. rundy układają się same według rozstawienia (zmienisz je w zakładce Drabinka). Tu wszystkie rundy dostaną godziny i boiska."
              : t.format === "GroupAndKnockout"
                ? "Mecze „każdy z każdym” w grupach, a po nich puchar z miejsc w grupach. Wszystkie mecze dostaną godziny od razu."
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
            manualBrackets={manualBrackets}
            lonelyCategories={lonelyCategories}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Generuj terminarz</CardTitle>
          <CardDescription>
            Po wygenerowaniu turniej przejdzie do etapu „Harmonogram gotowy”. Żeby ułożyć terminarz
            jeszcze raz, trzeba go najpierw wyczyścić.
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
              acceptedTeams={
                (teams.data ?? []).filter((team) => team.registrationStatus === "Accepted").length
              }
              waitingTeams={(teams.data ?? [])
                .filter((team) => team.registrationStatus === "Pending")
                .map((team) => team.teamName)}
              notReadyReason={
                registrationClosed
                  ? "Najpierw spełnij warunki z listy powyżej."
                  : t.status === "Draft"
                    ? "Zapisy jeszcze nie ruszyły. Otwórz je, a potem zamknij, żeby wygenerować terminarz."
                    : "Zapisy są wciąż otwarte. Zamknij je, żeby wygenerować terminarz."
              }
              beforeGenerate={t.format === "GroupAndKnockout" ? createGroupBrackets : undefined}
            />
          )}
        </CardContent>
      </Card>
    </div>
  )
}
