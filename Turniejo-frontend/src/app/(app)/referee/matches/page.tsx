import type { Metadata } from "next"

import { RefereeMatchesView } from "./referee-matches"

export const metadata: Metadata = { title: "Moje mecze" }

export default function RefereeMatchesPage() {
  return <RefereeMatchesView />
}
