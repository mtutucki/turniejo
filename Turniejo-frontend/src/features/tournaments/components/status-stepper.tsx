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
import { cn } from "@/lib/utils"
import {
  TOURNAMENT_STATUS_FLOW,
  TOURNAMENT_STATUS_LABELS,
  TOURNAMENT_STATUS_TRANSITIONS,
  type Tournament,
  TournamentStatus,
  type TournamentStatus as TournamentStatusName,
} from "@/types/tournaments"

import { useChangeTournamentStatus } from "../api"

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
  onConfirm,
  disabled,
}: {
  title: string
  description: string
  actionLabel: string
  destructive?: boolean
  onConfirm: () => void
  disabled?: boolean
}) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant={destructive ? "outline" : "default"} disabled={disabled}>
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
 * z Finished/Cancelled nie ma wyjścia.
 */
export function TournamentStatusStepper({ tournament, canManage = false }: StepperProps) {
  const changeStatus = useChangeTournamentStatus(tournament.id)
  const transitions = TOURNAMENT_STATUS_TRANSITIONS[tournament.status]
  const next = transitions.find((s) => s !== "Cancelled")
  const canCancel = transitions.includes("Cancelled")
  const isFinal = transitions.length === 0

  function change(status: TournamentStatusName) {
    changeStatus.mutate(status, {
      onSuccess: () => toast.success(`Status: ${TOURNAMENT_STATUS_LABELS[status]}`),
    })
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
                description={`Status turnieju zmieni się na „${TOURNAMENT_STATUS_LABELS[next]}”. Nie da się cofnąć do poprzedniego etapu.`}
                actionLabel={NEXT_STEP_LABELS[next] ?? TOURNAMENT_STATUS_LABELS[next]}
                onConfirm={() => change(next)}
                disabled={changeStatus.isPending}
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
              disabled={changeStatus.isPending}
            />
          )}
        </div>
      )}
    </div>
  )
}
