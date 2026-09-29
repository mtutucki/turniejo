import type { Metadata } from "next"

import { PageHeader } from "@/components/layout/page-header"

import { AccountOverview } from "./account-overview"

export const metadata: Metadata = { title: "Start" }

export default function DashboardPage() {
  return (
    <>
      <PageHeader title="Start" description="Twoje konto i role w Turniejo" />
      <AccountOverview />
    </>
  )
}
