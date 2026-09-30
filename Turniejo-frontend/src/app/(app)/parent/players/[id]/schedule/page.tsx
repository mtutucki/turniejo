import type { Metadata } from "next"

import { ChildScheduleView } from "../child-views"

export const metadata: Metadata = { title: "Terminarz" }

export default function Page() {
  return <ChildScheduleView />
}
