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
  /** Użytkownik, dla którego zapamiętano turniej. Inny user w tej samej przeglądarce go nie dziedziczy. */
  ownerId: string | null
  setTournament: (tournament: ActiveTournament | null) => void
  /** Przypina store do zalogowanego usera (null = wylogowany); przy zmianie usera czyści turniej. */
  syncOwner: (userId: string | null) => void
}

/**
 * Turniej, w kontekście którego działa nawigacja (Mecze / Tabela albo Drabinka / Drużyny).
 * Ustawiany przy wejściu w turniej (Faza 2), pamiętany między wizytami.
 */
export const useActiveTournamentStore = create<ActiveTournamentState>()(
  persist(
    (set) => ({
      tournament: null,
      ownerId: null,
      setTournament: (tournament) => set({ tournament }),
      syncOwner: (userId) =>
        set((state) =>
          state.ownerId === userId ? state : { ownerId: userId, tournament: null },
        ),
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
