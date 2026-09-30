import type { Metadata } from "next"

import { TournamentTeamsView } from "./tournament-teams"

export const metadata: Metadata = { title: "Drużyny turnieju" }

export default function TournamentTeamsPage() {
  return <TournamentTeamsView />
}
