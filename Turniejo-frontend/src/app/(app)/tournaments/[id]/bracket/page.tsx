import type { Metadata } from "next"

import { TournamentBracketView } from "./tournament-bracket"

export const metadata: Metadata = { title: "Drabinka" }

export default function TournamentBracketPage() {
  return <TournamentBracketView />
}
