import type { Metadata } from "next"

import { MyTeamsView } from "./my-teams"

export const metadata: Metadata = { title: "Moje drużyny" }

export default function MyTeamsPage() {
  return <MyTeamsView />
}
