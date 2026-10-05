import type { Metadata } from "next"

import { TournamentTeamDetails } from "./tournament-team-details"

export const metadata: Metadata = { title: "Skład drużyny" }

export default function TournamentTeamPage() {
  return <TournamentTeamDetails />
}
