import type { Metadata } from "next"

import { TournamentDashboardView } from "./tournament-dashboard"

export const metadata: Metadata = { title: "Przegląd turnieju" }

export default function TournamentDashboardPage() {
  return <TournamentDashboardView />
}
