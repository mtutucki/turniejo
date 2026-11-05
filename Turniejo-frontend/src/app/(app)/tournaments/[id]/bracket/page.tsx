import type { Metadata } from "next"
import { Suspense } from "react"

import { TournamentBracketView } from "./tournament-bracket"

export const metadata: Metadata = { title: "Drabinka" }

export default function TournamentBracketPage() {
  // useSearchParams (kategoria w ?category=) wymaga granicy Suspense przy prerenderze.
  return (
    <Suspense>
      <TournamentBracketView />
    </Suspense>
  )
}
