"use client"

import { ArrowRight, CalendarCheck, Check, CircleAlert, Network, Plus, Shield } from "lucide-react"
import Link from "next/link"
import { type ReactNode, useRef, useState } from "react"
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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useChangeRegistrationStatus } from "@/features/teams/api"
import { TeamFormDialog } from "@/features/teams/components/team-form-dialog"
import {
  useMyOrganization,
  usePublicSettings,
  useTournamentSettings,
} from "@/features/tournaments/api"
import { FormatForm } from "@/features/tournaments/components/format-form"
import { PublicSettingsForm } from "@/features/tournaments/components/public-settings-form"
import { SportSettingsForm } from "@/features/tournaments/components/sport-settings-form"
import { SETUP_STEP_KEYS, type SetupStepKey } from "@/features/tournaments/setup-steps"
import { useSetupProgress } from "@/features/tournaments/use-setup-progress"
import { useTournamentContext } from "@/features/tournaments/use-tournament-context"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import { TOURNAMENT_FORMAT_LABELS, statusAtLeast } from "@/types/tournaments"
import type { TournamentTeam } from "@/types/teams"

import { TournamentCategoriesView } from "../categories/tournament-categories"
import { TournamentPitchesView } from "../pitches/tournament-pitches"
import { TournamentTeamsView } from "../teams/tournament-teams"

const STEPS: Record<SetupStepKey, { title: string; description: string }> = {
  format: {
    title: "Format",
    description:
      "Jak drużyny będą ze sobą grać. Format możesz zmienić do czasu wygenerowania terminarza.",
  },
  rules: {
    title: "Zasady gry",
    description:
      "Punktacja i czas meczu. Domyślne wartości pasują do większości turniejów — zmień tylko to, co trzeba.",
  },
  portal: {
    title: "Portal kibica",
    description: "Publiczna strona z wynikami na żywo dla rodziców i kibiców, bez logowania.",
  },
  categories: { title: "Kategorie", description: "" },
  pitches: { title: "Boiska", description: "" },
  teams: { title: "Drużyny", description: "" },
  schedule: {
    title: "Terminarz",
    description: "Ostatni krok: sprawdź, czy wszystko jest, i ułóż mecze.",
  },
}

function StepIndicator({
  current,
  isDone,
  onSelect,
}: {
  current: number
  isDone: (key: SetupStepKey, index: number) => boolean
  onSelect: (index: number) => void
}) {
  return (
    <ol className="flex flex-wrap gap-x-1 gap-y-2" aria-label="Kroki konfiguracji">
      {SETUP_STEP_KEYS.map((key, index) => {
        const done = isDone(key, index)
        return (
          <li key={key} className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => onSelect(index)}
              aria-current={index === current ? "step" : undefined}
              className={cn(
                "hover:text-foreground flex items-center gap-2 rounded-full py-1 pr-3 pl-1 text-sm font-medium transition-colors",
                index === current ? "bg-primary/10 text-primary" : "text-muted-foreground",
              )}
            >
              <span
                className={cn(
                  "flex size-6 items-center justify-center rounded-full border text-xs tabular-nums",
                  index === current && "border-primary",
                  done && "bg-primary text-primary-foreground border-primary",
                )}
              >
                {done ? <Check className="size-3.5" aria-label="Zrobione" /> : index + 1}
              </span>
              {STEPS[key].title}
            </button>
            {index < SETUP_STEP_KEYS.length - 1 && (
              <span className="bg-border hidden h-px w-3 sm:block" aria-hidden />
            )}
          </li>
        )
      })}
    </ol>
  )
}

/** Organizator zgłasza własne drużyny — akceptowanie każdej osobno to tylko dodatkowe kliknięcia. */
function AcceptPendingTeams({
  tournamentId,
  teams,
}: {
  tournamentId: string
  teams: TournamentTeam[]
}) {
  const changeStatus = useChangeRegistrationStatus(tournamentId)
  const [pending, setPending] = useState(false)

  async function acceptAll() {
    setPending(true)
    let accepted = 0
    // Po kolei — limit drużyn backend sprawdza przy każdej akceptacji (błąd pokazuje globalny toast).
    for (const team of teams) {
      try {
        await changeStatus.mutateAsync({ tournamentTeamId: team.id, status: "Accepted" })
        accepted++
      } catch {
        break
      }
    }
    setPending(false)
    if (accepted > 0) {
      toast.success(`Zaakceptowano ${plural(accepted, ["drużynę", "drużyny", "drużyn"])}`)
    }
  }

  return (
    <div className="border-primary/40 bg-primary/5 flex flex-wrap items-center justify-between gap-3 rounded-xl border px-4 py-3 text-sm">
      <p>
        {plural(teams.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])} na akceptację —
        dopóki jej nie zaakceptujesz, nie zagra w turnieju.
      </p>
      <Button size="sm" onClick={acceptAll} disabled={pending}>
        {pending ? "Akceptowanie…" : "Akceptuj wszystkie"}
      </Button>
    </div>
  )
}

function ReadinessRow({
  ok,
  title,
  detail,
  onFix,
}: {
  ok: boolean
  title: string
  detail: string
  onFix: () => void
}) {
  return (
    <li className="flex items-center gap-3 py-3 text-sm">
      {ok ? (
        <span className="bg-primary text-primary-foreground flex size-5 shrink-0 items-center justify-center rounded-full">
          <Check className="size-3" aria-label="Gotowe" />
        </span>
      ) : (
        <CircleAlert className="text-live size-5 shrink-0" aria-label="Do uzupełnienia" />
      )}
      <span className="min-w-0 flex-1">
        <span className="block font-medium">{title}</span>
        <span className="text-muted-foreground block text-xs">{detail}</span>
      </span>
      <Button size="sm" variant={ok ? "ghost" : "outline"} onClick={onFix}>
        {ok ? "Zmień" : "Uzupełnij"}
      </Button>
    </li>
  )
}

/**
 * Kreator turnieju: od formatu po terminarz, w kolejności, w jakiej jedno zależy od drugiego
 * (drużyny potrzebują kategorii, terminarz — drużyn i boisk). To te same ekrany co w zakładkach,
 * ale po jednym naraz i z jasnym "co dalej". Postęp liczymy z danych, więc można wyjść i wrócić.
 */
export function TournamentSetupView({ initialStep }: { initialStep?: SetupStepKey }) {
  const { id, tournament, isOrganizer, rolesLoading } = useTournamentContext()
  const settings = useTournamentSettings(id)
  const publicSettings = usePublicSettings(id, isOrganizer)
  const organization = useMyOrganization()
  const progress = useSetupProgress(id, tournament.data)
  // null = dane jeszcze się wczytują: startujemy od kroku z linku albo pierwszego niegotowego.
  const [selected, setSelected] = useState<number | null>(
    initialStep ? SETUP_STEP_KEYS.indexOf(initialStep) : null,
  )
  const top = useRef<HTMLDivElement>(null)
  // Krok startowy ustalamy raz, gdy dane się wczytają. Liczony na bieżąco przeskakiwał sam do
  // następnego kroku w chwili, gdy obecny stawał się gotowy (np. po akceptacji drugiej drużyny).
  if (selected === null && !progress.isPending) {
    setSelected(SETUP_STEP_KEYS.indexOf(progress.firstIncomplete))
  }
  const data = tournament.data!
  const base = `/tournaments/${id}`

  if (rolesLoading || (selected === null && progress.isPending)) {
    return <Skeleton className="h-72 max-w-3xl rounded-xl" />
  }
  if (!isOrganizer) {
    return (
      <EmptyState
        icon={Shield}
        title="Konfigurację prowadzi organizator"
        description="Tylko organizator może zmieniać ustawienia turnieju."
      />
    )
  }

  const step = selected ?? SETUP_STEP_KEYS.indexOf(progress.firstIncomplete)
  const key = SETUP_STEP_KEYS[step]!
  const lastIndex = SETUP_STEP_KEYS.length - 1

  // Kolejny krok zaczyna się od góry — poprzedni formularz mógł być przewinięty do przycisku.
  function goTo(index: number) {
    setSelected(Math.max(0, Math.min(index, lastIndex)))
    top.current?.scrollIntoView({ block: "start" })
  }
  const goToKey = (target: SetupStepKey) => goTo(SETUP_STEP_KEYS.indexOf(target))
  const next = () => goTo(step + 1)

  // Te same blokady co w Ustawieniach (domena backendu).
  const formatLocked = statusAtLeast(data.status, "ScheduleGenerated")
  const rulesLocked = statusAtLeast(data.status, "InProgress")
  const knockout = data.format === "Knockout"

  // Zasad gry i portalu nie da się "odhaczyć" z danych (mają domyślne) — liczy się, że je minięto.
  const isDone = (stepKey: SetupStepKey, index: number) =>
    stepKey === "rules" || stepKey === "portal" ? index < step : progress.done[stepKey]

  // Czego brakuje, żeby pójść dalej z kroku z listą (kategorie, boiska, drużyny).
  // `consequence` trafia do potwierdzenia: sama szara linijka przy przycisku była za słabym sygnałem.
  const blocker: Partial<Record<SetupStepKey, { missing: string; consequence: string } | null>> = {
    categories: progress.done.categories
      ? null
      : {
          missing: "Dodaj co najmniej jedną kategorię",
          consequence:
            "Drużyny zgłasza się do kategorii — bez niej nie dodasz w następnych krokach żadnej drużyny.",
        },
    pitches: progress.done.pitches
      ? null
      : {
          missing: "Dodaj co najmniej jedno boisko",
          consequence:
            "Generator ustawia mecze na boiskach — bez boiska nie wygenerujesz terminarza.",
        },
    teams: progress.done.teams
      ? null
      : {
          missing: "Potrzeba co najmniej 2 zaakceptowanych drużyn",
          consequence:
            progress.pendingTeams.length > 0
              ? "Zgłoszone drużyny czekają na akceptację i na razie nie zagrają — bez dwóch zaakceptowanych nie ułożysz żadnego meczu."
              : "Bez dwóch zaakceptowanych drużyn nie ułożysz żadnego meczu ani terminarza.",
        },
  }
  const currentBlocker = blocker[key] ?? null
  const isListStep = key in blocker
  const isFormStep = key === "format" || key === "rules" || key === "portal"

  let body: ReactNode
  if (key === "categories") {
    body = <TournamentCategoriesView embedded />
  } else if (key === "pitches") {
    body = <TournamentPitchesView />
  } else if (key === "teams") {
    body = (
      <div className="space-y-4">
        {progress.pendingTeams.length > 0 && (
          <AcceptPendingTeams tournamentId={id} teams={progress.pendingTeams} />
        )}
        <TournamentTeamsView />
        {organization.data && progress.done.categories && (
          <p className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
            Drużyny nie ma jeszcze w bazie organizacji?
            <TeamFormDialog
              organizationId={organization.data.id}
              trigger={
                <Button size="sm" variant="outline">
                  <Plus />
                  Nowa drużyna
                </Button>
              }
            />
            Potem zgłosisz ją przyciskiem „Zgłoś drużynę”.
          </p>
        )}
      </div>
    )
  } else {
    const ready =
      progress.done.format &&
      progress.done.categories &&
      progress.done.pitches &&
      progress.done.teams
    const needsBracket = knockout && progress.matchCount === 0
    body = (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{STEPS[key].title}</CardTitle>
          <CardDescription>{STEPS[key].description}</CardDescription>
        </CardHeader>
        <CardContent>
          {key === "format" && (
            <FormatForm tournament={data} disabled={formatLocked} onSaved={next} />
          )}
          {key === "rules" &&
            (settings.data ? (
              <SportSettingsForm
                tournamentId={id}
                settings={settings.data}
                disabled={rulesLocked}
                onSaved={next}
              />
            ) : (
              <Skeleton className="h-72" />
            ))}
          {key === "portal" &&
            (publicSettings.data ? (
              <PublicSettingsForm
                tournamentId={id}
                slug={data.slug}
                settings={publicSettings.data}
                disabled={data.status === "Cancelled"}
                onSaved={next}
              />
            ) : (
              <Skeleton className="h-72" />
            ))}
          {key === "schedule" && (
            <div className="space-y-4">
              <ul className="divide-y">
                <ReadinessRow
                  ok={progress.done.format}
                  title="Format rozgrywek"
                  detail={
                    data.format ? TOURNAMENT_FORMAT_LABELS[data.format] : "Nie wybrano formatu"
                  }
                  onFix={() => goToKey("format")}
                />
                <ReadinessRow
                  ok={progress.done.categories}
                  title="Kategorie"
                  detail={
                    progress.categoryCount
                      ? plural(progress.categoryCount, ["kategoria", "kategorie", "kategorii"])
                      : "Brak kategorii — drużyny zgłasza się do kategorii"
                  }
                  onFix={() => goToKey("categories")}
                />
                <ReadinessRow
                  ok={progress.done.pitches}
                  title="Boiska"
                  detail={
                    progress.activePitches
                      ? plural(progress.activePitches, [
                          "aktywne boisko",
                          "aktywne boiska",
                          "aktywnych boisk",
                        ])
                      : "Brak boisk — generator nie ma gdzie ustawić meczów"
                  }
                  onFix={() => goToKey("pitches")}
                />
                <ReadinessRow
                  ok={progress.done.teams}
                  title="Drużyny"
                  detail={
                    `${plural(progress.acceptedTeams.length, ["zaakceptowana drużyna", "zaakceptowane drużyny", "zaakceptowanych drużyn"])}` +
                    (progress.pendingTeams.length
                      ? `, ${plural(progress.pendingTeams.length, ["czeka", "czekają", "czeka"])} na akceptację`
                      : "") +
                    (progress.done.teams ? "" : " — potrzeba co najmniej 2")
                  }
                  onFix={() => goToKey("teams")}
                />
              </ul>
              {progress.done.schedule ? (
                <div className="space-y-3">
                  <p className="text-sm">Terminarz jest gotowy — turniej może ruszać.</p>
                  <Button asChild>
                    <Link href={`${base}/schedule`}>
                      <CalendarCheck />
                      Zobacz terminarz
                    </Link>
                  </Button>
                </div>
              ) : !ready ? (
                <p className="text-muted-foreground text-sm">
                  Uzupełnij brakujące kroki, a tutaj pojawi się przycisk generowania terminarza.
                </p>
              ) : needsBracket ? (
                <div className="space-y-3">
                  <p className="text-muted-foreground text-sm">
                    Puchar zaczyna się od drabinki: ułóż pary 1. rundy, a potem nadasz im godziny i
                    boiska.
                  </p>
                  <Button asChild>
                    <Link href={`${base}/bracket`}>
                      <Network />
                      Utwórz drabinkę
                    </Link>
                  </Button>
                </div>
              ) : (
                <div className="space-y-3">
                  <p className="text-muted-foreground text-sm">
                    Wszystko jest. W generatorze zamkniesz zapisy, wybierzesz godziny i zobaczysz
                    mecze przed zapisaniem.
                  </p>
                  <Button asChild>
                    <Link href={`${base}/schedule/generate`}>
                      <CalendarCheck />
                      Generuj terminarz
                    </Link>
                  </Button>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    )
  }

  return (
    // Kroki z formularzem są wąskie; listy (kategorie, boiska, drużyny) potrzebują miejsca.
    <div ref={top} className={cn("scroll-mt-20 space-y-6", isListStep ? "max-w-5xl" : "max-w-3xl")}>
      <StepIndicator current={step} isDone={isDone} onSelect={goTo} />

      {body}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          {step > 0 && (
            <Button variant="ghost" size="sm" onClick={() => goTo(step - 1)}>
              Wstecz
            </Button>
          )}
          {isFormStep && (
            <Button variant="ghost" size="sm" onClick={next}>
              Pomiń ten krok
            </Button>
          )}
          <Button asChild variant="link" size="sm" className="text-muted-foreground">
            <Link href={`${base}/dashboard`}>
              {step === lastIndex ? "Przejdź do przeglądu" : "Pomiń konfigurację"}
            </Link>
          </Button>
        </div>
        {isListStep && (
          <div className="flex flex-wrap items-center gap-3">
            {currentBlocker ? (
              <>
                <span className="text-live flex items-center gap-1.5 text-sm font-medium">
                  <CircleAlert className="size-4 shrink-0" />
                  {currentBlocker.missing}
                </span>
                <AlertDialog>
                  <AlertDialogTrigger asChild>
                    <Button variant="outline">
                      Dalej mimo to
                      <ArrowRight />
                    </Button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Ten krok nie jest skończony</AlertDialogTitle>
                      <AlertDialogDescription>
                        {currentBlocker.consequence} Możesz iść dalej i wrócić tu później — krok
                        zostanie oznaczony jako niegotowy.
                      </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Zostaję i uzupełniam</AlertDialogCancel>
                      <AlertDialogAction onClick={next}>Dalej mimo to</AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              </>
            ) : (
              <Button onClick={next}>
                Dalej
                <ArrowRight />
              </Button>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
