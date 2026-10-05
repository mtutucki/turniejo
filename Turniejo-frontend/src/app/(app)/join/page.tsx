import type { Metadata } from "next"

import { JoinTournament } from "./join-tournament"

export const metadata: Metadata = { title: "Dołącz do turnieju" }

// ?code= przychodzi z linku zaproszenia od organizatora (panel "Kody dostępu").
export default async function JoinPage({ searchParams }: PageProps<"/join">) {
  const { code } = await searchParams
  const initialCode = (Array.isArray(code) ? code[0] : code)?.trim().toUpperCase() ?? ""
  return <JoinTournament initialCode={/^[0-9A-F]{6}$/.test(initialCode) ? initialCode : ""} />
}
