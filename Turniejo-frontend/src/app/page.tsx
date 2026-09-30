import { redirect } from "next/navigation"

import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"

export default function HomePage() {
  redirect(DEFAULT_APP_PATH)
}
