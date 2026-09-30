import { SearchX } from "lucide-react"
import type { Metadata } from "next"
import Link from "next/link"

import { Button } from "@/components/ui/button"

export const metadata: Metadata = { title: "Nie znaleziono" }

export default function NotFound() {
  return (
    <div className="mx-auto flex max-w-md flex-1 flex-col items-center justify-center gap-4 px-4 py-16 text-center">
      <SearchX className="text-muted-foreground size-10" />
      <div className="space-y-1">
        <h1 className="text-lg font-semibold">Nie ma takiej strony</h1>
        <p className="text-muted-foreground text-sm">
          Link mógł wygasnąć albo turniej nie jest publiczny.
        </p>
      </div>
      <Button asChild variant="outline">
        <Link href="/">Strona główna</Link>
      </Button>
    </div>
  )
}
