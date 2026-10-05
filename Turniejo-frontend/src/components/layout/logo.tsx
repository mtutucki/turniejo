import { Trophy } from "lucide-react"
import Link from "next/link"

import { DEFAULT_APP_PATH } from "@/lib/auth/public-paths"

export function Logo() {
  return (
    <Link href={DEFAULT_APP_PATH} className="flex items-center gap-2 font-semibold tracking-tight">
      <span className="bg-primary text-primary-foreground flex size-7 items-center justify-center rounded-md">
        <Trophy className="size-4" />
      </span>
      Turniejo
    </Link>
  )
}
