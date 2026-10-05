import type { Metadata } from "next"

import { JoinedTournaments } from "./joined-tournaments"

export const metadata: Metadata = { title: "Dołączone turnieje" }

export default function JoinedTournamentsPage() {
  return <JoinedTournaments />
}
