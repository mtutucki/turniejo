"use client"

import { BellOff, CheckCheck, Trash2 } from "lucide-react"
import { AnimatePresence, motion } from "motion/react"

import { EmptyState } from "@/components/layout/empty-state"
import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import {
  useDeleteNotification,
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
} from "@/features/notifications/notification-sync"
import { formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useNotificationStore } from "@/stores/notification-store"
import { NOTIFICATION_TYPE_LABELS, type AppNotification } from "@/types/roles-panels"

const isUnread = (n: AppNotification) => !n.readAt && n.status !== "Read"

export function NotificationsView() {
  const query = useNotifications()
  // Lista ze store — "przeczytane" znika od razu po kliknięciu, zanim wróci odpowiedź.
  const notifications = useNotificationStore((state) => state.notifications)
  const live = useNotificationStore((state) => state.live)
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllNotificationsRead()
  const deleteNotification = useDeleteNotification()

  const sorted = [...notifications].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  const unreadIds = sorted.filter(isUnread).map((n) => n.notificationRecipientId)

  return (
    <div>
      <PageHeader
        title="Powiadomienia"
        description={
          live
            ? "Nowe powiadomienia pojawiają się na bieżąco."
            : "Łączenie z powiadomieniami na żywo…"
        }
        actions={
          unreadIds.length > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => markAll.mutate(unreadIds)}
              disabled={markAll.isPending}
            >
              <CheckCheck />
              Oznacz wszystkie
            </Button>
          )
        }
      />
      {query.isPending ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : !sorted.length ? (
        <EmptyState
          icon={BellOff}
          title="Brak powiadomień"
          description="Tu zobaczysz ogłoszenia i zmiany w meczach."
        />
      ) : (
        <ul
          className="bg-card divide-y overflow-hidden rounded-xl border"
          data-testid="notification-list"
        >
          <AnimatePresence initial={false}>
            {sorted.map((n) => {
              const unread = isUnread(n)
              return (
                <motion.li
                  // Nowe powiadomienie (SignalR) wjeżdża z góry; pierwsze wczytanie listy bez animacji.
                  layout
                  initial={{ opacity: 0, y: -12 }}
                  animate={{ opacity: 1, y: 0 }}
                  key={n.notificationRecipientId}
                  data-unread={unread || undefined}
                  className={cn("flex gap-3 px-4 py-3", unread && "bg-primary/5")}
                >
                  <span
                    aria-hidden
                    className={cn(
                      "mt-2 size-2 shrink-0 rounded-full",
                      unread ? "bg-primary" : "bg-transparent",
                    )}
                  />
                  <div className="min-w-0 flex-1 space-y-1">
                    <p className="text-muted-foreground flex flex-wrap items-center gap-x-2 text-xs">
                      <span className="text-primary font-medium">
                        {NOTIFICATION_TYPE_LABELS[n.type] ?? n.type}
                      </span>
                      <span className="font-mono tabular-nums">{formatDateTime(n.createdAt)}</span>
                    </p>
                    <p className={cn("text-sm", unread ? "font-semibold" : "font-medium")}>
                      {n.title}
                    </p>
                    <p className="text-muted-foreground text-sm whitespace-pre-line">{n.message}</p>
                    {unread && (
                      <Button
                        variant="link"
                        size="sm"
                        className="h-auto px-0"
                        onClick={() => markRead.mutate(n.notificationRecipientId)}
                      >
                        Oznacz jako przeczytane
                      </Button>
                    )}
                  </div>
                  {!unread && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="text-muted-foreground hover:text-destructive shrink-0"
                      aria-label="Usuń powiadomienie"
                      title="Usuń powiadomienie"
                      onClick={() => deleteNotification.mutate(n.notificationRecipientId)}
                    >
                      <Trash2 />
                    </Button>
                  )}
                </motion.li>
              )
            })}
          </AnimatePresence>
        </ul>
      )}
    </div>
  )
}
