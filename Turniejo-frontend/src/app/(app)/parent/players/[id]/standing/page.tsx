import type { Metadata } from "next"

import { ChildStandingView } from "../child-views"

export const metadata: Metadata = { title: "Tabela" }

export default function Page() {
  return <ChildStandingView />
}
