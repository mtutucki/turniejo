import type { Metadata } from "next"

import { ComingSoon } from "@/components/layout/coming-soon"

export const metadata: Metadata = { title: "Turnieje" }

export default function TournamentsPage() {
  return <ComingSoon title="Turnieje" phase={2} />
}
