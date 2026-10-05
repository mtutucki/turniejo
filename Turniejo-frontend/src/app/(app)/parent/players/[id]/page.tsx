import { redirect } from "next/navigation"

export default async function ChildPage({ params }: PageProps<"/parent/players/[id]">) {
  const { id } = await params
  redirect(`/parent/players/${id}/schedule`)
}
