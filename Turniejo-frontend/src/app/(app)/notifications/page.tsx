import type { Metadata } from "next"

import { NotificationsView } from "./notifications-view"

export const metadata: Metadata = { title: "Powiadomienia" }

export default function NotificationsPage() {
  return <NotificationsView />
}
