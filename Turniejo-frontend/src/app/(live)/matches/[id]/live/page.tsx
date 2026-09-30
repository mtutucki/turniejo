import type { Metadata } from "next"

import { LiveMatchView } from "./live-match"

export const metadata: Metadata = { title: "Mecz na żywo" }

export default function LiveMatchPage() {
  return <LiveMatchView />
}
