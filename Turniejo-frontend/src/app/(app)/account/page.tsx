import type { Metadata } from "next"

import { AccountView } from "./account-view"

export const metadata: Metadata = { title: "Moje konto" }

export default function AccountPage() {
  return <AccountView />
}
