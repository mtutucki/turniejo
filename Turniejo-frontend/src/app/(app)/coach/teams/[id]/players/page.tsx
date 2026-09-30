import type { Metadata } from "next"

import { CoachPlayersView } from "../team-views"

export const metadata: Metadata = { title: "Skład" }

export default function Page() {
  return <CoachPlayersView />
}
