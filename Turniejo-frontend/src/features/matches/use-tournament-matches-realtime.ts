"use client"

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useQueryClient } from "@tanstack/react-query"
import { useEffect, useRef } from "react"

import { useApi } from "@/hooks/use-api"

import { matchKeys } from "./api"
import { liveKeys } from "./live-api"

// Przez rewrite /backend, token w query stringu (jak w use-match-realtime): niepubliczne turnieje
// wpuszczają do grupy tylko swoich członków.
const HUB_URL = "/backend/hubs/tournaments"

/**
 * Zmiany meczów, tabel i drabinki w turniejach zalogowanego usera (TournamentHub, zdarzenia MatchChanged,
 * StandingUpdated i BracketUpdated): wzięcie,
 * oddanie i przydzielenie sędziego, start, wynik, koniec. Odświeża listy sędziego (moje i wolne mecze)
 * oraz terminarz turnieju, więc sędziowie i organizator widzą nawzajem swoje zmiany bez odświeżania.
 */
export function useTournamentMatchesRealtime(tournamentIds: string[]) {
  const queryClient = useQueryClient()
  const { getToken, isAuthenticated } = useApi()

  // Odświeżenie sesji zmienia getToken, a nie powinno zrywać połączenia.
  const getTokenRef = useRef(getToken)
  useEffect(() => {
    getTokenRef.current = getToken
  }, [getToken])

  // Stabilny klucz: ta sama lista turniejów w innej kolejności nie otwiera nowego połączenia.
  const key = [...new Set(tournamentIds.map((id) => id.toLowerCase()))].sort().join(",")

  useEffect(() => {
    const ids = key ? key.split(",") : []
    if (!isAuthenticated || ids.length === 0) return

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: async () => (await getTokenRef.current()) ?? "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.None)
      .build()

    // Odświeżają się tylko otwarte widoki, więc unieważnienie wszystkich terminarzy jest tanie
    // (i nie zależy od wielkości liter w identyfikatorze z backendu).
    const refresh = () => {
      // liveKeys.assigned jest prefiksem liveKeys.open, więc jedno unieważnienie odświeża obie listy.
      void queryClient.invalidateQueries({ queryKey: liveKeys.assigned })
      for (const id of ids) {
        void queryClient.invalidateQueries({ queryKey: matchKeys.schedule(id) })
        // Wynik meczu pucharowego i awans zwycięzcy widać też na drabince organizatora.
        void queryClient.invalidateQueries({ queryKey: ["tournaments", id, "bracket"] })
      }
    }

    connection.on("MatchChanged", refresh)
    // Awans, korekta, uzupełnienie slotów z grup: bez tego drabinka organizatora czekała na odświeżenie strony.
    connection.on("BracketUpdated", () => {
      for (const id of ids)
        void queryClient.invalidateQueries({ queryKey: ["tournaments", id, "bracket"] })
    })

    // Tabela przelicza się po zakończonym meczu (później niż MatchChanged), więc czeka na własne zdarzenie.
    connection.on("StandingUpdated", () => {
      for (const id of ids)
        void queryClient.invalidateQueries({ queryKey: ["tournaments", id, "standings"] })
    })

    // Brak dostępu do jednego turnieju (np. usunięty) nie blokuje pozostałych.
    const join = () =>
      Promise.allSettled(ids.map((id) => connection.invoke("JoinTournamentGroup", id)))

    // Po przerwie w połączeniu zdarzenia mogły przepaść, więc pobieramy wszystko od nowa.
    connection.onreconnected(() => void join().then(() => refresh()))

    let cancelled = false
    connection
      .start()
      .then(() => (cancelled ? undefined : join()))
      .catch(() => undefined)

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [key, isAuthenticated, queryClient])
}
