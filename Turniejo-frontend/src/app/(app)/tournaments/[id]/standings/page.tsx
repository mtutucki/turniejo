import type { Metadata } from "next"

import { TournamentStandingsView } from "./tournament-standings"

export const metadata: Metadata = { title: "Tabela" }

export default function TournamentStandingsPage() {
  return <TournamentStandingsView />
}
