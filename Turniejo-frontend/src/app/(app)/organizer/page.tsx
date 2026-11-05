import type { Metadata } from "next"

import { BecomeOrganizerView } from "./become-organizer-view"

export const metadata: Metadata = { title: "Zostań organizatorem" }

export default function BecomeOrganizerPage() {
  return <BecomeOrganizerView />
}
