import type { Metadata } from "next"

import { CoachStatsView } from "../team-views"

export const metadata: Metadata = { title: "Statystyki" }

export default function Page() {
  return <CoachStatsView />
}
