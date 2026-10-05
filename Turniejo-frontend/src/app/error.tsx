"use client"

import { ErrorFallback } from "@/components/layout/error-fallback"

export default function RootError(props: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  return <ErrorFallback {...props} />
}
