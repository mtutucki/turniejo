import type { Metadata } from "next"
import { Suspense } from "react"

import { RefereeMatchesView } from "./referee-matches"

export const metadata: Metadata = { title: "Moje mecze" }

export default function RefereeMatchesPage() {
  // useSearchParams (wybrany turniej w ?tournament=) wymaga granicy Suspense przy prerenderze.
  return (
    <Suspense>
      <RefereeMatchesView />
    </Suspense>
  )
}
