"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"

import { isNavItemActive, NAV_ITEMS } from "./nav-items"

export function BottomNav() {
  const pathname = usePathname()

  return (
    <nav
      aria-label="Nawigacja główna"
      className="bg-background/95 fixed inset-x-0 bottom-0 z-30 border-t pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden"
    >
      <ul className="grid h-16" style={{ gridTemplateColumns: `repeat(${NAV_ITEMS.length}, 1fr)` }}>
        {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
          const active = isNavItemActive(pathname, href)
          return (
            <li key={href}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex h-full flex-col items-center justify-center gap-1 text-xs font-medium transition-colors",
                  active ? "text-foreground" : "text-muted-foreground hover:text-foreground",
                )}
              >
                <Icon className={cn("size-5", active && "text-primary")} />
                {label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
