"use client"

import { Shield } from "lucide-react"
import { useRouter } from "next/navigation"
import { useEffect } from "react"

import { EmptyState } from "@/components/layout/empty-state"
import { Skeleton } from "@/components/ui/skeleton"
import { useMyOrganization } from "@/features/tournaments/api"

/** Skrót z nawigacji: baza drużyn własnej organizacji (/organizations/[id]/teams). */
export default function TeamsShortcutPage() {
  const router = useRouter()
  const organization = useMyOrganization()
  const organizationId = organization.data?.id

  useEffect(() => {
    if (organizationId) router.replace(`/organizations/${organizationId}/teams`)
  }, [organizationId, router])

  if (organization.data === null) {
    return (
      <EmptyState
        icon={Shield}
        title="Nie masz organizacji"
        description="Baza drużyn należy do organizacji, której jesteś organizatorem."
      />
    )
  }
  return <Skeleton className="h-40 rounded-xl" />
}
