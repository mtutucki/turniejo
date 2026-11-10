"use client"

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useQueryClient } from "@tanstack/react-query"
import { useEffect, useState } from "react"

import type { RealtimeState } from "@/features/matches/use-match-realtime"

import { publicKeys } from "./api"

// TournamentHub dopuszcza anonimowych widzów publicznych turniejów (RealtimeAccessGuard) — bez tokenu.
const HUB_URL = "/backend/hubs/tournaments"

/**
 * Subskrypcja grupy turnieju na portalu kibica:
 * - MatchChanged (gol, start, pauza, koniec) → lista meczów i karta LIVE,
 * - StandingUpdated → tabele (animacja zmiany pozycji),
 * - BracketUpdated → drabinka.
 * Po ponownym połączeniu odświeżamy wszystko — zdarzenia z czasu przerwy mogły przepaść.
 */
export function useTournamentRealtime(slug: string, tournamentId: string) {
  const queryClient = useQueryClient()
  const [state, setState] = useState<RealtimeState>("connecting")

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.None)
      .build()

    const invalidate = (key: readonly unknown[]) =>
      void queryClient.invalidateQueries({ queryKey: key })

    connection.on("MatchChanged", () => {
      invalidate(publicKeys.schedule(slug))
      invalidate([...publicKeys.all(slug), "live"])
    })
    connection.on("StandingUpdated", () => invalidate([...publicKeys.all(slug), "standings"]))
    connection.on("BracketUpdated", () => {
      invalidate([...publicKeys.all(slug), "bracket"])
      // Dwie fazy grupowe: drużyny trafiły na miejsca fazy 2 i powstały ich mecze.
      invalidate([...publicKeys.all(slug), "second-stage"])
      invalidate(publicKeys.schedule(slug))
    })

    const join = async () => {
      await connection.invoke("JoinTournamentGroup", tournamentId)
      setState("connected")
    }

    connection.onreconnecting(() => setState("reconnecting"))
    connection.onreconnected(() => {
      void join()
        .then(() => invalidate(publicKeys.all(slug)))
        .catch(() => setState("disconnected"))
    })
    connection.onclose(() => setState("disconnected"))

    let cancelled = false
    connection
      .start()
      .then(() => (cancelled ? undefined : join()))
      .catch(() => {
        if (!cancelled) setState("disconnected")
      })

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [queryClient, slug, tournamentId])

  return state
}
