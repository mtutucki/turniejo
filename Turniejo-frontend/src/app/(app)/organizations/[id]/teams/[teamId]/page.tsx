import type { Metadata } from "next"

import { TeamRosterPage } from "./team-roster-page"

export const metadata: Metadata = { title: "Kadra drużyny" }

export default function Page() {
  return <TeamRosterPage />
}
