import { create } from "zustand"

import type { AppNotification } from "@/types/roles-panels"

type NotificationState = {
  notifications: AppNotification[]
  /** Połączenie z NotificationHub — dzwonek pokazuje, czy powiadomienia przychodzą na żywo. */
  live: boolean
  setNotifications: (notifications: AppNotification[]) => void
  markRead: (notificationRecipientId: string) => void
  setLive: (live: boolean) => void
  reset: () => void
}

/**
 * Globalny stan powiadomień: lista z GET /api/notifications (NotificationSync) + znacznik "przeczytane"
 * nakładany od razu po kliknięciu. Dzwonek w nagłówku czyta z tego licznik nieprzeczytanych.
 */
export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  live: false,
  setNotifications: (notifications) => set({ notifications }),
  markRead: (id) =>
    set((state) => ({
      notifications: state.notifications.map((n) =>
        n.notificationRecipientId === id
          ? { ...n, status: "Read", readAt: n.readAt ?? new Date().toISOString() }
          : n,
      ),
    })),
  setLive: (live) => set({ live }),
  reset: () => set({ notifications: [], live: false }),
}))

export const selectUnreadCount = (state: NotificationState) =>
  state.notifications.filter((n) => !n.readAt && n.status !== "Read").length
