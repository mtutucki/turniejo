import type { Metadata } from "next"

import { TournamentPitchesView } from "./tournament-pitches"

export const metadata: Metadata = { title: "Boiska" }

export default function TournamentPitchesPage() {
  return <TournamentPitchesView />
}
