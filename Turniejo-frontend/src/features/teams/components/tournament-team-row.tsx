"use client"

import { ChevronRight } from "lucide-react"
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  PAYMENT_STATUS_LABELS,
  type PaymentStatus,
  REGISTRATION_STATUS_LABELS,
  REGISTRATION_TRANSITIONS,
  type RegistrationStatus,
  type TournamentTeam,
} from "@/types/teams"
import type { TournamentGroup } from "@/types/tournaments"

import { useAssignTeamToGroup, useChangePaymentStatus, useChangeRegistrationStatus } from "../api"
import { RegistrationStatusBadge } from "./registration-status-badge"
import { TeamLogo } from "./team-logo"

const NO_GROUP = "none"
const PAYMENT_STATUSES = Object.keys(PAYMENT_STATUS_LABELS) as PaymentStatus[]

const TRANSITION_ACTIONS: Record<
  RegistrationStatus,
  { label: string; variant: "default" | "outline" }
> = {
  Accepted: { label: "Akceptuj", variant: "default" },
  Rejected: { label: "Odrzuć", variant: "outline" },
  Withdrawn: { label: "Wycofaj", variant: "outline" },
  Pending: { label: "Przywróć", variant: "outline" },
}

type TournamentTeamRowProps = {
  tournamentId: string
  team: TournamentTeam
  groups: TournamentGroup[]
  canManage: boolean
  /** Komplet zaakceptowanych drużyn: backend i tak odrzuci akceptację. */
  limitReached?: boolean
  /** Lista jest już podzielona na grupy: nazwa grupy przy drużynie byłaby powtórzeniem. */
  inGroupSection?: boolean
  /** Terminarz już jest: mecze powstały z tych grup, backend odrzuci zmianę grupy i akceptację. */
  scheduleLocked?: boolean
  /** Terminarz już jest: wycofanie odwoła mecze drużyny, więc „Wycofaj” najpierw pyta z tym tekstem. */
  withdrawWarning?: string
}

export function TournamentTeamRow({
  tournamentId,
  team,
  groups,
  canManage,
  limitReached = false,
  inGroupSection = false,
  scheduleLocked = false,
  withdrawWarning,
}: TournamentTeamRowProps) {
  const changeStatus = useChangeRegistrationStatus(tournamentId)
  const changePayment = useChangePaymentStatus(tournamentId)
  const assignGroup = useAssignTeamToGroup(tournamentId)
  const transitions = REGISTRATION_TRANSITIONS[team.registrationStatus]
  const groupName = groups.find((g) => g.id === team.groupId)?.name

  function setStatus(status: RegistrationStatus) {
    changeStatus.mutate(
      { tournamentTeamId: team.id, status },
      { onSuccess: () => toast.success(`${team.teamName}: ${REGISTRATION_STATUS_LABELS[status]}`) },
    )
  }

  return (
    <li className="flex flex-col gap-3 py-3 lg:flex-row lg:items-center">
      <Link
        href={`/tournaments/${tournamentId}/teams/${team.id}`}
        className="hover:text-primary group flex min-w-0 flex-1 items-center gap-3"
      >
        <TeamLogo name={team.teamName} logoUrl={team.teamLogoUrl} />
        <span className="min-w-0">
          <span className="block truncate font-medium">{team.teamName}</span>
          <span className="text-muted-foreground flex items-center gap-2 text-xs">
            <RegistrationStatusBadge status={team.registrationStatus} />
            {!canManage && !inGroupSection && groupName && <span>{groupName}</span>}
          </span>
        </span>
        <ChevronRight className="text-muted-foreground ml-auto size-4 lg:hidden" />
      </Link>

      {canManage && (
        <div className="flex flex-wrap items-center gap-2">
          {/* Grupa ma sens tylko dla drużyn, które zagrają (oczekujące lub zaakceptowane). Przy jednej
              grupie w kategorii drużyna trafia do niej sama — wybór tylko, gdy jest z czego wybierać
              (albo drużyna z czasów przed domyślną grupą wisi bez grupy). */}
          {(groups.length > 1 || (groups.length === 1 && !team.groupId)) &&
            (team.registrationStatus === "Pending" || team.registrationStatus === "Accepted") && (
              <Select
                value={team.groupId ?? NO_GROUP}
                onValueChange={(value) =>
                  assignGroup.mutate(
                    { tournamentTeamId: team.id, groupId: value === NO_GROUP ? null : value },
                    { onSuccess: () => toast.success(`${team.teamName}: grupa zapisana`) },
                  )
                }
                disabled={assignGroup.isPending || scheduleLocked}
              >
                <SelectTrigger
                  size="sm"
                  className="w-36"
                  aria-label={`Grupa: ${team.teamName}`}
                  title={scheduleLocked ? "Grupę zmienisz po wyczyszczeniu terminarza" : undefined}
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_GROUP}>Bez grupy</SelectItem>
                  {groups.map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          <Select
            value={team.paymentStatus}
            onValueChange={(value) =>
              changePayment.mutate(
                { tournamentTeamId: team.id, status: value as PaymentStatus },
                { onSuccess: () => toast.success(`${team.teamName}: opłata zapisana`) },
              )
            }
            disabled={changePayment.isPending}
          >
            <SelectTrigger size="sm" className="w-36" aria-label={`Opłata: ${team.teamName}`}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAYMENT_STATUSES.map((s) => (
                <SelectItem key={s} value={s}>
                  {PAYMENT_STATUS_LABELS[s]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {transitions.map((status) => {
            const blocked = status === "Accepted" && (limitReached || scheduleLocked)
            if (status === "Withdrawn" && withdrawWarning)
              return (
                <AlertDialog key={status}>
                  <AlertDialogTrigger asChild>
                    <Button
                      size="sm"
                      variant={TRANSITION_ACTIONS[status].variant}
                      disabled={changeStatus.isPending}
                    >
                      {TRANSITION_ACTIONS[status].label}
                    </Button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Wycofać drużynę {team.teamName}?</AlertDialogTitle>
                      <AlertDialogDescription>{withdrawWarning}</AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Anuluj</AlertDialogCancel>
                      <AlertDialogAction variant="destructive" onClick={() => setStatus(status)}>
                        Wycofaj
                      </AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              )
            return (
              <Button
                key={status}
                size="sm"
                variant={TRANSITION_ACTIONS[status].variant}
                disabled={changeStatus.isPending || blocked}
                title={
                  !blocked
                    ? undefined
                    : scheduleLocked
                      ? "Drużynę przyjmiesz po wyczyszczeniu terminarza"
                      : "Limit drużyn jest wyczerpany"
                }
                onClick={() => setStatus(status)}
              >
                {TRANSITION_ACTIONS[status].label}
              </Button>
            )
          })}
          <Button asChild size="sm" variant="ghost" className="hidden lg:inline-flex">
            <Link href={`/tournaments/${tournamentId}/teams/${team.id}`}>
              Skład
              <ChevronRight />
            </Link>
          </Button>
        </div>
      )}
    </li>
  )
}
