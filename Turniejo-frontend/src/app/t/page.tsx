import type { Metadata } from "next"

import { PublicTournamentsPage } from "@/features/public/components/public-tournaments-page"

export const metadata: Metadata = {
  title: "Turnieje publiczne",
  description: "Wyniki, terminarze i tabele turniejów piłkarskich na żywo.",
}

export default async function PublicTournamentsListPage({ searchParams }: PageProps<"/t">) {
  return <PublicTournamentsPage searchParams={await searchParams} />
}
