import type { Metadata } from "next"

import { NewTournament } from "./new-tournament"

export const metadata: Metadata = { title: "Nowy turniej" }

export default function NewTournamentPage() {
  return <NewTournament />
}
