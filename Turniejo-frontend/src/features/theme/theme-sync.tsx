"use client"

import { useSession } from "next-auth/react"
import { useTheme } from "next-themes"
import { useEffect, useRef } from "react"

import { useMyProfile } from "@/features/account/api"

/**
 * Po zalogowaniu motyw z konta wygrywa z zapisanym w przeglądarce (docs/decyzje.md), raz na konto:
 * dalsze zmiany robi przełącznik, który zapisuje je i tu, i na koncie.
 */
export function ThemeSync() {
  const { data: session } = useSession()
  const { data: profile } = useMyProfile()
  const { setTheme } = useTheme()
  const appliedFor = useRef<string | null>(null)

  const userId = session?.user?.id ?? null
  const accountTheme = profile?.theme
  useEffect(() => {
    if (!userId || !accountTheme || appliedFor.current === userId) return
    appliedFor.current = userId
    setTheme(accountTheme)
  }, [userId, accountTheme, setTheme])

  return null
}
