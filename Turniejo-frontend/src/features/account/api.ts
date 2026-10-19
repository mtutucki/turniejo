"use client"

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import { useApi } from "@/hooks/use-api"

/** GET /api/me/export — kopia danych konta (RODO art. 15/20) zapisywana jako plik JSON. */
export function useExportMyData() {
  const { request } = useApi()
  return useMutation({
    mutationFn: async () => {
      const data = await request<unknown>("/api/me/export")
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" })
      const url = URL.createObjectURL(blob)
      const link = document.createElement("a")
      link.href = url
      link.download = `turniejo-moje-dane-${new Date().toISOString().slice(0, 10)}.json`
      link.click()
      URL.revokeObjectURL(url)
    },
  })
}

/**
 * DELETE /api/me — anonimizacja konta i usunięcie logowania. 409 "Conflict.Account" = jedyny organizator:
 * konto znika dopiero z `deleteOrganization`, czyli razem z organizacją i jej danymi.
 */
export function useDeleteMyAccount() {
  const { request } = useApi()
  return useMutation({
    mutationFn: ({ deleteOrganization = false }: { deleteOrganization?: boolean } = {}) =>
      request(deleteOrganization ? "/api/me?deleteOrganization=true" : "/api/me", {
        method: "DELETE",
      }),
    meta: { handlesConflict: true },
  })
}

export type MyProfile = {
  email: string
  firstName: string
  lastName: string
  phoneNumber: string | null
}

export const accountKeys = {
  profile: ["me", "profile"] as const,
}

/** GET /api/me/profile — dane z bazy (token nie ma telefonu i nie odświeża się po zmianie). */
export function useMyProfile() {
  const { request, isAuthenticated } = useApi()
  return useQuery({
    queryKey: accountKeys.profile,
    queryFn: () => request<MyProfile>("/api/me/profile"),
    enabled: isAuthenticated,
  })
}

export type UpdateMyProfile = Pick<MyProfile, "firstName" | "lastName" | "phoneNumber">

/** PUT /api/me/profile — imię, nazwisko i telefon. E-mail jest loginem, więc się nie zmienia. */
export function useUpdateMyProfile() {
  const { request } = useApi()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateMyProfile) => request("/api/me/profile", { method: "PUT", body }),
    // Imię i nazwisko widać też u organizatora (członkowie turnieju) i w sztabie drużyn.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["me"] }),
    meta: { handlesValidation: true },
  })
}

/** POST /api/me/password — wymaga obecnego hasła; backend zamyka potem wszystkie sesje. */
export function useChangeMyPassword() {
  const { request } = useApi()
  return useMutation({
    mutationFn: (body: { currentPassword: string; newPassword: string }) =>
      request("/api/me/password", { method: "POST", body }),
    meta: { handlesValidation: true },
  })
}
