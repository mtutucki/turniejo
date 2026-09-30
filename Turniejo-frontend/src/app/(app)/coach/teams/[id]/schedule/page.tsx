import type { Metadata } from "next"

import { CoachScheduleView } from "../team-views"

export const metadata: Metadata = { title: "Terminarz" }

export default function Page() {
  return <CoachScheduleView />
}
