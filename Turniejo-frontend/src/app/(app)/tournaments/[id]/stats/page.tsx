import type { Metadata } from "next"

import { TournamentStatsView } from "./tournament-stats"

export const metadata: Metadata = { title: "Statystyki" }

export default function TournamentStatsPage() {
  return <TournamentStatsView />
}
