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
import { formatDateTime, formatTime, plural, toZonedDateInput, zonedToIso } from "@/lib/format"
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
    date: z.string().min(1, "Wybierz dzień"),
    startTime: z.string().min(1, "Podaj godzinę rozpoczęcia"),
    endTime: z.string().min(1, "Podaj godzinę zakończenia"),
    pitchIds: z.array(z.string()).min(1, "Wybierz co najmniej jedno boisko"),
  })
  .refine((v) => v.endTime > v.startTime, {
    path: ["endTime"],
    message: "Koniec musi być po rozpoczęciu",
  })

type GenerateValues = z.infer<typeof schema>

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
            Po zamknięciu nie zgłosisz już nowych drużyn. Nie da się cofnąć do poprzedniego etapu.
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
        <Requirement ok={bracketMatches > 0}>
          {bracketMatches > 0 ? (
            `Drabinka gotowa — ${plural(bracketMatches, ["mecz", "mecze", "meczów"])} 1. rundy do rozstawienia`
          ) : (
            <>
              Drabinka z parami 1. rundy{" "}
              <Link href={`/tournaments/${id}/bracket`} className="text-primary underline">
                (utwórz drabinkę)
              </Link>
            </>
          )}
        </Requirement>
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
  const form = useForm<GenerateValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      date: toZonedDateInput(tournament.startAt),
      startTime: "09:00",
      endTime: "18:00",
      pitchIds: pitches.map((p) => p.id),
    },
  })
  const { errors } = form.formState
  const [date, startTime, endTime, pitchIds] = useWatch({
    control: form.control,
    name: ["date", "startTime", "endTime", "pitchIds"],
  })

  // Boisko dodane już po otwarciu strony (np. przyciskiem "Dodaj Boisko 1" w warunkach) ma być od
  // razu zaznaczone — defaultValues zapamiętały listę z chwili montowania formularza.
  const knownPitches = useRef(new Set(pitches.map((p) => p.id)))
  useEffect(() => {
    const added = pitches.filter((p) => !knownPitches.current.has(p.id)).map((p) => p.id)
    if (added.length === 0) return
    added.forEach((id) => knownPitches.current.add(id))
    form.setValue("pitchIds", [...form.getValues("pitchIds"), ...added], { shouldValidate: true })
  }, [pitches, form])

  const { totalMatches, maxRounds } = workload
  const slotMinutes = settings.matchDurationMinutes + settings.breakDurationMinutes
  // Szacunek: tyle slotów, ile wymaga liczba boisk, ale nie mniej niż kolejek w największej grupie
  // (drużyna nie gra dwóch meczów naraz). Generator może potrzebować trochę więcej.
  const slots = pitchIds.length ? Math.max(Math.ceil(totalMatches / pitchIds.length), maxRounds) : 0
  const estimatedEnd =
    date && startTime && slots
      ? new Date(
          new Date(zonedToIso(date, startTime)).getTime() +
            ((slots - 1) * slotMinutes + settings.matchDurationMinutes) * 60_000,
        )
      : null
  const fitsWindow =
    estimatedEnd && endTime ? estimatedEnd <= new Date(zonedToIso(date, endTime)) : true

  function submit(values: GenerateValues) {
    const startAt = zonedToIso(values.date, values.startTime)
    const endAt = zonedToIso(values.date, values.endTime)
    // Backend sprawdza to samo — tu tylko po to, żeby błąd pojawił się przy polach.
    if (startAt < new Date(tournament.startAt).toISOString()) {
      form.setError("startTime", {
        message: `Turniej zaczyna się ${formatDateTime(tournament.startAt)}`,
      })
      return
    }
    if (endAt > new Date(tournament.endAt).toISOString()) {
      form.setError("endTime", {
        message: `Turniej kończy się ${formatDateTime(tournament.endAt)}`,
      })
      return
    }
    generate.mutate(
      {
        startAt,
        endAt,
        pitchIds: values.pitchIds,
      },
      {
        onSuccess: ({ matchesCreated }) => {
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
        <div className="grid gap-4 sm:grid-cols-3">
          <TextField
            id="schedule-date"
            label="Dzień"
            type="date"
            min={toZonedDateInput(tournament.startAt)}
            max={toZonedDateInput(tournament.endAt)}
            error={errors.date}
            {...form.register("date")}
          />
          <TextField
            id="schedule-start"
            label="Pierwszy mecz"
            type="time"
            className="font-mono tabular-nums"
            error={errors.startTime}
            {...form.register("startTime")}
          />
          <TextField
            id="schedule-end"
            label="Koniec (ostatni mecz musi się skończyć)"
            type="time"
            className="font-mono tabular-nums"
            error={errors.endTime}
            {...form.register("endTime")}
          />
        </div>

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
          {estimatedEnd && (
            <p className={cn("text-muted-foreground", !fitsWindow && "text-destructive")}>
              Szacowany koniec ok.{" "}
              <span className="font-mono tabular-nums">{formatTime(estimatedEnd)}</span>
              {!fitsWindow &&
                " — nie zmieści się w wybranych godzinach. Dodaj boisko albo wydłuż dzień."}
            </p>
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
            <Link href={`/tournaments/${tournament.id}/schedule`}>Anuluj</Link>
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
      <Link href={`/tournaments/${id}/schedule`}>
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
