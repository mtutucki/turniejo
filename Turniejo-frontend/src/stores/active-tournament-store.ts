import { create } from "zustand"
import { createJSONStorage, persist } from "zustand/middleware"

import type { TournamentFormat } from "@/types/tournaments"

export type ActiveTournament = {
  id: string
  name: string
  /** Decyduje o sekcji nawigacji: puchar ma drabinkę zamiast tabeli. Brak = jeszcze nieznany. */
  format?: TournamentFormat | null
}

type ActiveTournamentState = {
  tournament: ActiveTournament | null
  setTournament: (tournament: ActiveTournament | null) => void
}

/**
 * Turniej, w kontekście którego działa nawigacja (Mecze / Tabela albo Drabinka / Drużyny).
 * Ustawiany przy wejściu w turniej (Faza 2), pamiętany między wizytami.
 */
export const useActiveTournamentStore = create<ActiveTournamentState>()(
  persist(
    (set) => ({
      tournament: null,
      setTournament: (tournament) => set({ tournament }),
    }),
    {
      name: "turniejo-active-tournament",
      storage: createJSONStorage(() => localStorage),
      // Serwer renderuje bez localStorage — odczyt dopiero po hydracji (Providers), inaczej
      // HTML z serwera i pierwszy render klienta by się różniły.
      skipHydration: true,
    },
  ),
)
