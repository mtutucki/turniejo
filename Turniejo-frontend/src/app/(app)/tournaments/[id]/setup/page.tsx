import type { Metadata } from "next"

import { TournamentSetupView } from "./tournament-setup"

export const metadata: Metadata = { title: "Konfiguracja turnieju" }

export default function TournamentSetupPage() {
  return <TournamentSetupView />
}
