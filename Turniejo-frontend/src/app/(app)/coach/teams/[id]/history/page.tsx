import type { Metadata } from "next"

import { CoachHistoryView } from "../team-views"

export const metadata: Metadata = { title: "Wszystkie turnieje" }

export default function Page() {
  return <CoachHistoryView />
}
