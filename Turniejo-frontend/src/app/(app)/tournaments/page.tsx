import type { Metadata } from "next"

import { TournamentList } from "./tournament-list"

export const metadata: Metadata = { title: "Moje turnieje" }

export default function TournamentsPage() {
  return <TournamentList />
}
