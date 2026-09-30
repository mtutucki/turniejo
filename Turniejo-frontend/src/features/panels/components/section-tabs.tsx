"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"

/** Zakładki podstron panelu (dziecko/drużyna) — ten sam styl co zakładki turnieju. */
export function SectionTabs({
  base,
  tabs,
}: {
  base: string
  tabs: { segment: string; label: string }[]
}) {
  const pathname = usePathname()
  return (
    <nav aria-label="Sekcje" className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0">
      <ul className="flex min-w-max gap-1 border-b">
        {tabs.map(({ segment, label }) => {
          const href = `${base}/${segment}`
          const active = pathname.startsWith(href)
          return (
            <li key={segment}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "-mb-px block border-b-2 px-3 py-2 text-sm font-medium transition-colors",
                  active
                    ? "border-primary text-primary"
                    : "text-muted-foreground hover:text-foreground border-transparent",
                )}
              >
                {label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
