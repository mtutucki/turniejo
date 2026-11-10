import type { Metadata } from "next"

import { TournamentTeamDetails } from "./tournament-team-details"

export const metadata: Metadata = { title: "Drużyna" }

export default function TournamentTeamPage() {
  return <TournamentTeamDetails />
}
