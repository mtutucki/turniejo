import { FlaskConical } from "lucide-react"
import Link from "next/link"

import { IS_DEMO } from "@/lib/demo"

/** Pasek nad treścią strony, widoczny tylko w instancji demo (NEXT_PUBLIC_DEMO_MODE=true). */
export function DemoBanner() {
  if (!IS_DEMO) return null
  return (
    <div
      role="note"
      className="flex items-center justify-center gap-2 bg-amber-400/15 px-4 py-1.5 text-center text-xs text-amber-950 dark:text-amber-100"
    >
      <FlaskConical className="size-3.5 shrink-0" aria-hidden />
      <p>
        <strong className="font-semibold">Wersja demo.</strong> Nie wpisuj prawdziwych danych.
        Wszystko może zniknąć bez uprzedzenia.{" "}
        <Link href="/terms#demo" className="font-medium underline underline-offset-2">
          Szczegóły
        </Link>
      </p>
    </div>
  )
}
