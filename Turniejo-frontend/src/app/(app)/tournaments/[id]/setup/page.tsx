import type { Metadata } from "next"

import { TournamentSetupView } from "./tournament-setup"

export const metadata: Metadata = { title: "Konfiguracja turnieju" }

// Krok kreatora (?step=…) czyta sam widok — patrz stepFromUrl.
export default function TournamentSetupPage() {
  return <TournamentSetupView />
}
