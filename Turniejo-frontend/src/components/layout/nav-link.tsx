"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"

import { cn } from "@/lib/utils"
import { selectUnreadCount, useNotificationStore } from "@/stores/notification-store"

import { isNavItemActive, type NavItem } from "./nav-items"

/** Pozycja nawigacji w sidebarze i w arkuszu "Więcej". */
export function NavLink({
  href,
  label,
  icon: Icon,
  activePattern,
  showsUnread = false,
  onNavigate,
  disabled = false,
}: NavItem & { onNavigate?: () => void; disabled?: boolean }) {
  const pathname = usePathname()
  const unread = useNotificationStore(selectUnreadCount)
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
      {showsUnread && unread > 0 && (
        <span
          className="bg-live text-live-foreground ml-auto flex min-w-5 items-center justify-center rounded-full px-1 font-mono text-[11px] leading-5 font-semibold tabular-nums"
          aria-label={`${unread} nieprzeczytane`}
        >
          {unread > 99 ? "99+" : unread}
        </span>
      )}
    </Link>
  )
}
