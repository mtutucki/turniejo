"use client"

import { ErrorFallback } from "@/components/layout/error-fallback"

// W obrębie powłoki aplikacji — sidebar i nawigacja zostają, pada tylko treść strony.
export default function AppError(props: { error: Error & { digest?: string }; reset: () => void }) {
  return <ErrorFallback {...props} homeHref="/tournaments" homeLabel="Moje turnieje" />
}
