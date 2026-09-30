import type { Metadata } from "next"

import { NewPlayer } from "./new-player"

export const metadata: Metadata = { title: "Nowy zawodnik" }

export default function NewPlayerPage() {
  return <NewPlayer />
}
