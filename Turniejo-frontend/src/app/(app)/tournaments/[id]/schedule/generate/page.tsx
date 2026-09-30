import type { Metadata } from "next"

import { GenerateScheduleView } from "./generate-schedule"

export const metadata: Metadata = { title: "Generuj terminarz" }

export default function GenerateSchedulePage() {
  return <GenerateScheduleView />
}
