import type { Metadata } from "next"

import { ComingSoon } from "@/components/layout/coming-soon"

export const metadata: Metadata = { title: "Moje mecze" }

export default function RefereeMatchesPage() {
  return <ComingSoon title="Moje mecze" phase={5} />
}
