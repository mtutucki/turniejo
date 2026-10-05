import { Construction } from "lucide-react"

import { PageHeader } from "./page-header"

/** Tymczasowa zawartość sekcji nawigacji, które powstaną w kolejnych fazach. */
export function ComingSoon({ title, phase }: { title: string; phase: number }) {
  return (
    <>
      <PageHeader title={title} />
      <div className="text-muted-foreground flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed py-16 text-center">
        <Construction className="size-8" />
        <p className="text-sm">Ta sekcja powstanie w Fazie {phase}.</p>
      </div>
    </>
  )
}
