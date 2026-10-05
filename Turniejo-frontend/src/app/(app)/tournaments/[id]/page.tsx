import { redirect } from "next/navigation"

export default async function TournamentPage({ params }: PageProps<"/tournaments/[id]">) {
  const { id } = await params
  redirect(`/tournaments/${id}/dashboard`)
}
