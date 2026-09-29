import type { Metadata } from "next"

import { ComingSoon } from "@/components/layout/coming-soon"

export const metadata: Metadata = { title: "Dołącz do turnieju" }

export default function JoinPage() {
  return <ComingSoon title="Dołącz do turnieju" phase={8} />
}
