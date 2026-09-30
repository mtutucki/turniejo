"use client"

import { Bell } from "lucide-react"
import Link from "next/link"

import { cn } from "@/lib/utils"
import { selectUnreadCount, useNotificationStore } from "@/stores/notification-store"

/** Dzwonek w nagłówku z licznikiem nieprzeczytanych — licznik aktualizuje się na żywo (NotificationSync). */
export function NotificationBell({ className }: { className?: string }) {
  const unread = useNotificationStore(selectUnreadCount)
  const label = unread > 0 ? `Powiadomienia: ${unread} nieprzeczytane` : "Powiadomienia"

  return (
    <Link
      href="/notifications"
      aria-label={label}
      title={label}
      className={cn(
        "relative flex size-10 items-center justify-center rounded-full transition-colors hover:bg-white/10",
        className,
      )}
    >
      <Bell className="size-5" />
      {unread > 0 && (
        <span
          className="bg-live text-live-foreground absolute top-1 right-1 flex min-w-5 items-center justify-center rounded-full px-1 font-mono text-[11px] leading-5 font-semibold tabular-nums"
          data-testid="unread-count"
        >
          {unread > 99 ? "99+" : unread}
        </span>
      )}
    </Link>
  )
}
