"use client"

import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { plural } from "@/lib/format"
import type { TournamentTeam } from "@/types/teams"

import { useChangeRegistrationStatus } from "../api"

/** Organizator zgłasza własne drużyny, więc akceptowanie każdej osobno to tylko dodatkowe kliknięcia. */
export function AcceptPendingTeams({
  tournamentId,
  teams,
  freeSlots,
}: {
  tournamentId: string
  /** Oczekujące drużyny w kolejności z listy. */
  teams: TournamentTeam[]
  /** Ile drużyn zmieści się jeszcze w limicie (większe od zera). */
  freeSlots: number
}) {
  const changeStatus = useChangeRegistrationStatus(tournamentId)
  const [pending, setPending] = useState(false)
  const toAccept = Math.min(freeSlots, teams.length)

  async function acceptAll() {
    setPending(true)
    let accepted = 0
    // Po kolei: limit drużyn backend sprawdza przy każdej akceptacji (błąd pokazuje globalny toast).
    for (const team of teams.slice(0, toAccept)) {
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
        {plural(teams.length, ["drużyna czeka", "drużyny czekają", "drużyn czeka"])} na akceptację.
        Dopóki jej nie zaakceptujesz, nie zagra w turnieju.
        {toAccept < teams.length && (
          <span className="text-muted-foreground block text-xs">
            W limicie zmieści się jeszcze {plural(freeSlots, ["drużyna", "drużyny", "drużyn"])}.
          </span>
        )}
      </p>
      <Button size="sm" onClick={acceptAll} disabled={pending}>
        {pending
          ? "Akceptowanie…"
          : toAccept < teams.length
            ? `Akceptuj ${plural(toAccept, ["drużynę", "drużyny", "drużyn"])}`
            : "Akceptuj wszystkie"}
      </Button>
    </div>
  )
}
