"use client"

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useQueryClient } from "@tanstack/react-query"
import { useEffect, useRef, useState } from "react"

import { useApi } from "@/hooks/use-api"
import type { MatchLivePanel } from "@/types/matches"

import { invalidateLive, liveKeys } from "./live-api"

export type RealtimeState = "connecting" | "connected" | "reconnecting" | "disconnected"

// Przez rewrite /backend (backend nie ma CORS). Token idzie w query stringu (?access_token=),
// bo przeglądarkowy WebSocket nie pozwala ustawić nagłówka Authorization.
const HUB_URL = "/backend/hubs/matches"

/**
 * Subskrypcja grupy meczu na MatchHub. Każde zdarzenie serwera (gol, kartka, cofnięcie, zmiana statusu)
 * odświeża snapshot panelu; wynik z MatchScoreChanged wpisujemy od razu. Po zerwaniu połączenia
 * i ponownym połączeniu pobieramy pełny snapshot — zdarzenia z czasu przerwy mogły przepaść.
 */
export function useMatchRealtime(matchId: string, enabled: boolean) {
  const queryClient = useQueryClient()
  const { getToken } = useApi()
  const [state, setState] = useState<RealtimeState>("connecting")

  // getToken zmienia tożsamość przy odświeżeniu sesji — trzymamy najnowszy w refie,
  // żeby odświeżenie tokenu nie zrywało połączenia.
  const getTokenRef = useRef(getToken)
  useEffect(() => {
    getTokenRef.current = getToken
  }, [getToken])

  useEffect(() => {
    if (!enabled) return

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: async () => (await getTokenRef.current()) ?? "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      // Zerwania połączenia to normalna sytuacja na boisku (słaby zasięg) — stan pokazuje wskaźnik
      // w panelu, a automatyczne ponowne łączenie działa samo; bez tego konsola zapełnia się błędami.
      .configureLogging(LogLevel.None)
      .build()

    const refresh = () => invalidateLive(queryClient, matchId)
    const forThisMatch = (payload: { matchId?: string }) => payload.matchId === matchId

    connection.on(
      "MatchScoreChanged",
      (payload: { matchId: string; homeScore: number; awayScore: number }) => {
        if (!forThisMatch(payload)) return
        queryClient.setQueryData<MatchLivePanel>(liveKeys.panel(matchId), (panel) =>
          panel ? { ...panel, homeScore: payload.homeScore, awayScore: payload.awayScore } : panel,
        )
      },
    )
    for (const name of ["MatchStatusChanged", "MatchEventAdded", "MatchEventReverted"]) {
      connection.on(name, (payload: { matchId?: string }) => {
        if (forThisMatch(payload)) refresh()
      })
    }

    const join = async () => {
      await connection.invoke("JoinMatchGroup", matchId)
      setState("connected")
      refresh()
    }

    connection.onreconnecting(() => setState("reconnecting"))
    connection.onreconnected(() => {
      void join().catch(() => setState("disconnected"))
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
  }, [enabled, matchId, queryClient])

  return state
}
