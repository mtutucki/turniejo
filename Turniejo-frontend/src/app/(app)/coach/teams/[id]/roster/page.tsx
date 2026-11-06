import type { Metadata } from "next"

import { CoachRosterView } from "../team-views"

export const metadata: Metadata = { title: "Kadra" }

export default function Page() {
  return <CoachRosterView />
}
