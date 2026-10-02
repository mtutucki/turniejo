"use client"

import { Check, Lock } from "lucide-react"
import Link from "next/link"
import { toast } from "sonner"

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
import { useClearSchedule } from "@/features/matches/api"
import { plural } from "@/lib/format"
import { cn } from "@/lib/utils"
import {
  TOURNAMENT_STATUS_FLOW,
  TOURNAMENT_STATUS_LABELS,
  TOURNAMENT_STATUS_REVERT,
  TOURNAMENT_STATUS_TRANSITIONS,
  type Tournament,
  TournamentStatus,
  type TournamentStatus as TournamentStatusName,
} from "@/types/tournaments"

import { useChangeTournamentStatus, useTournamentDashboard } from "../api"

type StepperProps = {
  tournament: Tournament
  /** Pokazuje przyciski przejść (tylko organizator). */
  canManage?: boolean
}

/** Etapy, których nie ustawia się ręcznie — przycisk prowadzi do dedykowanej akcji. */
const MANUAL_BLOCKED: Partial<Record<TournamentStatusName, { label: string; path: string }>> = {
  // Backend ustawia ScheduleGenerated sam, po wygenerowaniu terminarza.
  ScheduleGenerated: { label: "Wygeneruj terminarz", path: "schedule/generate" },
}

const NEXT_STEP_LABELS: Partial<Record<TournamentStatusName, string>> = {
  RegistrationOpen: "Otwórz zapisy",
  RegistrationClosed: "Zamknij zapisy",
  InProgress: "Rozpocznij turniej",
  Finished: "Zakończ turniej",
}

function Steps({ status }: { status: TournamentStatusName }) {
  const current = TournamentStatus[status]
  const cancelled = status === "Cancelled"

  return (
    <ol className="flex flex-wrap gap-x-1 gap-y-2" aria-label="Etapy turnieju">
      {TOURNAMENT_STATUS_FLOW.map((step, index) => {
        const value = TournamentStatus[step]
        const done = !cancelled && value < current
        const active = !cancelled && value === current
        return (
          <li key={step} className="flex items-center gap-1">
            <span
              aria-current={active ? "step" : undefined}
              className={cn(
                "flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium",
                done && "border-primary/40 text-primary",
                active && "border-primary bg-primary text-primary-foreground",
                !done && !active && "text-muted-foreground",
              )}
            >
              {done && <Check className="size-3" />}
              {TOURNAMENT_STATUS_LABELS[step]}
            </span>
            {index < TOURNAMENT_STATUS_FLOW.length - 1 && (
              <span className="bg-border h-px w-3" aria-hidden />
            )}
          </li>
        )
      })}
    </ol>
  )
}

function ConfirmTransition({
  title,
  description,
  actionLabel,
  destructive,
  secondary,
  onConfirm,
  disabled,
}: {
  title: string
  description: string
  actionLabel: string
  destructive?: boolean
  /** Akcja poboczna (cofnięcie etapu) — przycisk bez wypełnienia. */
  secondary?: boolean
  onConfirm: () => void
  disabled?: boolean
}) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button
          variant={secondary ? "ghost" : destructive ? "outline" : "default"}
          disabled={disabled}
        >
          {actionLabel}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Anuluj</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            className={cn(destructive && "bg-destructive hover:bg-destructive/90 text-white")}
          >
            {actionLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/**
 * Maszyna stanów turnieju. Przyciski pokazują tylko przejścia dozwolone przez domenę backendu;
 * z Finished/Cancelled nie ma wyjścia. Do rozpoczęcia turnieju każdy etap da się cofnąć o jeden krok.
 */
export function TournamentStatusStepper({ tournament, canManage = false }: StepperProps) {
  const changeStatus = useChangeTournamentStatus(tournament.id)
  const clearSchedule = useClearSchedule(tournament.id)
  const previous = TOURNAMENT_STATUS_REVERT[tournament.status]
  const pending = changeStatus.isPending || clearSchedule.isPending
  const transitions = TOURNAMENT_STATUS_TRANSITIONS[tournament.status]
  const next = transitions.find((s) => s !== "Cancelled")
  const canCancel = transitions.includes("Cancelled")
  const isFinal = transitions.length === 0
  // Zakończenie turnieju anuluje mecze, które się nie odbyły (trwające blokują je po stronie backendu).
  const dashboard = useTournamentDashboard(tournament.id)
  const unplayed = dashboard.data?.matches.scheduled ?? 0
  const transitionDescription = (status: TournamentStatusName) =>
    `Status turnieju zmieni się na „${TOURNAMENT_STATUS_LABELS[status]}”.` +
    // Od rozpoczęcia turnieju nie ma już drogi wstecz; wcześniejsze etapy można cofnąć.
    (status === "InProgress" || status === "Finished"
      ? " Nie da się cofnąć do poprzedniego etapu."
      : "") +
    (status === "Finished" && unplayed > 0
      ? ` ${plural(unplayed, ["mecz nie został rozegrany i zostanie anulowany", "mecze nie zostały rozegrane i zostaną anulowane", "meczów nie zostało rozegranych i zostanie anulowanych"])}.`
      : "")

  function change(status: TournamentStatusName) {
    changeStatus.mutate(status, {
      onSuccess: () => toast.success(`Status: ${TOURNAMENT_STATUS_LABELS[status]}`),
    })
  }

  function revert(status: TournamentStatusName) {
    // Z "Harmonogram gotowy" cofa wyczyszczenie terminarza — backend sam zmienia wtedy etap.
    if (tournament.status !== "ScheduleGenerated") return change(status)
    clearSchedule.mutate(undefined, {
      onSuccess: () => toast.success(`Status: ${TOURNAMENT_STATUS_LABELS[status]}`),
    })
  }

  const revertCopy: Partial<Record<TournamentStatusName, { label: string; description: string }>> =
    {
      Draft: {
        label: "Wróć do szkicu",
        description:
          "Zapisy zostaną wstrzymane, a turniej wróci do szkicu. Zgłoszone drużyny zostają.",
      },
      RegistrationOpen: {
        label: "Otwórz zapisy ponownie",
        description:
          "Znów będzie można zgłaszać drużyny. Zamknij zapisy ponownie przed wygenerowaniem terminarza.",
      },
      RegistrationClosed: {
        label: "Wyczyść terminarz",
        description:
          tournament.format === "Knockout"
            ? "Turniej wróci do etapu „Zapisy zamknięte”. Mecze drabinki zostają — terminy i boiska nadasz im ponownie, generując terminarz."
            : "Wszystkie mecze zostaną usunięte razem z przypisaniami sędziów, a turniej wróci do etapu „Zapisy zamknięte”. Potem wygenerujesz terminarz od nowa.",
      },
    }

  return (
    <div className="space-y-4">
      <Steps status={tournament.status} />

      {tournament.status === "Cancelled" && (
        <p className="text-destructive text-sm">Turniej został anulowany.</p>
      )}

      {canManage && isFinal && (
        <p className="text-muted-foreground flex items-center gap-2 text-sm">
          <Lock className="size-4" />
          Status jest ostateczny — nie można go już zmienić.
        </p>
      )}

      {canManage && !isFinal && (
        <div className="flex flex-wrap items-center gap-2">
          {next && MANUAL_BLOCKED[next] ? (
            <Button asChild>
              <Link href={`/tournaments/${tournament.id}/${MANUAL_BLOCKED[next].path}`}>
                {MANUAL_BLOCKED[next].label}
              </Link>
            </Button>
          ) : (
            next && (
              <ConfirmTransition
                title={`${NEXT_STEP_LABELS[next] ?? TOURNAMENT_STATUS_LABELS[next]}?`}
                description={transitionDescription(next)}
                actionLabel={NEXT_STEP_LABELS[next] ?? TOURNAMENT_STATUS_LABELS[next]}
                onConfirm={() => change(next)}
                disabled={pending}
              />
            )
          )}
          {canCancel && (
            <ConfirmTransition
              title="Anulować turniej?"
              description="Anulowanego turnieju nie można przywrócić ani edytować."
              actionLabel="Anuluj turniej"
              destructive
              onConfirm={() => change("Cancelled")}
              disabled={pending}
            />
          )}
          {previous && revertCopy[previous] && (
            <ConfirmTransition
              title={`${revertCopy[previous].label}?`}
              description={revertCopy[previous].description}
              actionLabel={revertCopy[previous].label}
              secondary
              destructive={tournament.status === "ScheduleGenerated"}
              onConfirm={() => revert(previous)}
              disabled={pending}
            />
          )}
        </div>
      )}
    </div>
  )
}
