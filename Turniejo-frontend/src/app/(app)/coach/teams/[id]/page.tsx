import { redirect } from "next/navigation"

export default async function CoachTeamPage({ params }: PageProps<"/coach/teams/[id]">) {
  const { id } = await params
  redirect(`/coach/teams/${id}/players`)
}
