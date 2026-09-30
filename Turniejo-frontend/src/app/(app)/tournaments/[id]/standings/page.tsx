import type { Metadata } from "next"

import { ComingSoon } from "@/components/layout/coming-soon"

export const metadata: Metadata = { title: "Tabela" }

export default function TournamentStandingsPage() {
  return <ComingSoon title="Tabela" phase={6} />
}
