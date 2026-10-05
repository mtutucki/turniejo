"use client"

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useEffect, useRef } from "react"
import { toast } from "sonner"

import { useApi } from "@/hooks/use-api"
import { useNotificationStore } from "@/stores/notification-store"
import type { AppNotification } from "@/types/roles-panels"

export const notificationKeys = { all: ["notifications"] as const }

export function useNotifications() {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: notificationKeys.all,
    queryFn: () => request<AppNotification[]>("/api/notifications"),
    enabled: isAuthenticated,
    staleTime: 30_000,
  })
}

export function useMarkNotificationRead() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  const markRead = useNotificationStore((state) => state.markRead)
  return useMutation({
    mutationFn: (notificationRecipientId: string) =>
      request(`/api/notifications/${notificationRecipientId}/read`, { method: "PATCH" }),
    // Od razu w stanie globalnym — licznik na dzwonku spada bez czekania na odpowiedź.
    onMutate: (id) => markRead(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

/** Usuwa przeczytane powiadomienie z listy użytkownika (backend odrzuca nieprzeczytane). */
export function useDeleteNotification() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  const remove = useNotificationStore((state) => state.remove)
  return useMutation({
    mutationFn: (notificationRecipientId: string) =>
      request(`/api/notifications/${notificationRecipientId}`, { method: "DELETE" }),
    // Znika od razu; przy błędzie odświeżenie listy przywróci wpis.
    onMutate: (id) => remove(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

/** Backend nie ma endpointu zbiorczego — PATCH dla każdego nieprzeczytanego (zwykle kilka sztuk). */
export function useMarkAllNotificationsRead() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  const markRead = useNotificationStore((state) => state.markRead)
  return useMutation({
    mutationFn: async (ids: string[]) => {
      ids.forEach(markRead)
      await Promise.all(
        ids.map((id) => request(`/api/notifications/${id}/read`, { method: "PATCH" })),
      )
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

/** Ogłoszenie organizatora — trafia do organizatorów, sędziów, sztabów i opiekunów zawodników turnieju. */
export function useCreateAnnouncement(tournamentId: string) {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { title: string; message: string }) =>
      request<{ notificationId: string; recipientCount: number }>(
        `/api/tournaments/${tournamentId}/announcements`,
        { method: "POST", body: input },
      ),
    meta: { handlesValidation: true },
    // Organizator sam też bywa odbiorcą — odśwież jego listę.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

/**
 * Montowane raz w Providers: synchronizuje listę powiadomień z Zustand i słucha NotificationHub.
 * Nowe powiadomienie (NotificationCreated) → toast + odświeżenie listy (licznik na dzwonku).
 */
export function NotificationSync() {
  const { getToken, isAuthenticated } = useApi()
  const queryClient = useQueryClient()
  const notifications = useNotifications()
  const setNotifications = useNotificationStore((state) => state.setNotifications)
  const setLive = useNotificationStore((state) => state.setLive)
  const reset = useNotificationStore((state) => state.reset)

  const getTokenRef = useRef(getToken)
  useEffect(() => {
    getTokenRef.current = getToken
  }, [getToken])

  useEffect(() => {
    if (!isAuthenticated) reset()
    else if (notifications.data) setNotifications(notifications.data)
  }, [isAuthenticated, notifications.data, reset, setNotifications])

  useEffect(() => {
    if (!isAuthenticated) return

    const connection = new HubConnectionBuilder()
      .withUrl("/backend/hubs/notifications", {
        accessTokenFactory: async () => (await getTokenRef.current()) ?? "",
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build()

    connection.on("NotificationCreated", (payload: { title: string; message: string }) => {
      toast.info(payload.title, { description: payload.message })
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
    })
    connection.onreconnecting(() => setLive(false))
    connection.onreconnected(() => {
      setLive(true)
      // Powiadomienia z czasu przerwy są już w bazie — dociągamy listę.
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
    })
    connection.onclose(() => setLive(false))

    let cancelled = false
    connection
      .start()
      .then(() => {
        if (!cancelled) setLive(true)
      })
      .catch(() => setLive(false))

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [isAuthenticated, queryClient, setLive])

  return null
}
