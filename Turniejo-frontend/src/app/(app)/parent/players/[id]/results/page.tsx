import type { Metadata } from "next"

import { ChildResultsView } from "../child-views"

export const metadata: Metadata = { title: "Wyniki" }

export default function Page() {
  return <ChildResultsView />
}
