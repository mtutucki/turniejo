"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"

import { isNavItemActive, type NavItem } from "./nav-items"

/** Pozycja nawigacji w sidebarze i w arkuszu "Więcej". */
export function NavLink({
  href,
  label,
  icon: Icon,
  activePattern,
  onNavigate,
  disabled = false,
}: NavItem & { onNavigate?: () => void; disabled?: boolean }) {
  const pathname = usePathname()
  const active = !disabled && isNavItemActive(pathname, { href, activePattern })

  if (disabled) {
    return (
      <span
        aria-disabled="true"
        className="text-muted-foreground/50 flex cursor-not-allowed items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium"
      >
        <Icon className="size-4" />
        {label}
      </span>
    )
  }

  return (
    <Link
      href={href}
      onClick={onNavigate}
      aria-current={active ? "page" : undefined}
      className={cn(
        "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
        "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
        active && "bg-primary/10 text-primary hover:bg-primary/15 hover:text-primary",
      )}
    >
      <Icon className="size-4" />
      {label}
    </Link>
  )
}
