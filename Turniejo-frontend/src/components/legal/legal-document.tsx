import type { ReactNode } from "react"

import { isPlaceholder, LEGAL_VERSION } from "@/lib/legal"

/** Wartość z src/lib/legal.ts — nieuzupełniony placeholder jest wyróżniony, żeby go nie przeoczyć. */
export function Fill({ value }: { value: string }) {
  if (!isPlaceholder(value)) return <>{value}</>
  return (
    <mark className="rounded bg-amber-200 px-1 text-amber-950 dark:bg-amber-400/30 dark:text-amber-100">
      {value}
    </mark>
  )
}

export function LegalDocument({ title, children }: { title: string; children: ReactNode }) {
  return (
    <article className="space-y-8">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        <p className="text-muted-foreground text-sm">Wersja z dnia {LEGAL_VERSION}</p>
      </header>
      {children}
    </article>
  )
}

export function LegalSection({
  id,
  title,
  children,
}: {
  id?: string
  title: string
  children: ReactNode
}) {
  return (
    <section id={id} className="scroll-mt-20 space-y-3">
      <h2 className="text-lg font-semibold">{title}</h2>
      <div className="text-muted-foreground [&_strong]:text-foreground space-y-3 text-sm leading-relaxed [&_li]:pl-1 [&_ol]:list-decimal [&_ol]:space-y-1 [&_ol]:pl-5 [&_ul]:list-disc [&_ul]:space-y-1 [&_ul]:pl-5">
        {children}
      </div>
    </section>
  )
}
