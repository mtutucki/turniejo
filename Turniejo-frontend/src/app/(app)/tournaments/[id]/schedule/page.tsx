import type { Metadata } from "next"

import { TournamentScheduleView } from "./tournament-schedule"

export const metadata: Metadata = { title: "Terminarz" }

export default function TournamentSchedulePage() {
  return <TournamentScheduleView />
}
