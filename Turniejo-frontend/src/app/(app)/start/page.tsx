import type { Metadata } from "next"

import { StartView } from "./start-view"

export const metadata: Metadata = { title: "Start" }

export default function StartPage() {
  return <StartView />
}
