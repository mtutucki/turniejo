"use client"

import { useParams } from "next/navigation"

import { ErrorFallback } from "@/components/layout/error-fallback"

// Pod nagłówkiem portalu — kibic zostaje na stronie turnieju i może wrócić do jego startu.
export default function PublicError(props: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  const { slug } = useParams<{ slug: string }>()
  return <ErrorFallback {...props} homeHref={`/t/${slug}`} homeLabel="Strona turnieju" />
}
