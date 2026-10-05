"use client"

import { ErrorFallback } from "@/components/layout/error-fallback"

export default function LiveError(props: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  return <ErrorFallback {...props} homeHref="/referee/matches" homeLabel="Moje mecze" />
}
