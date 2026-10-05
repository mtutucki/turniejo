"use client"

import { useMutation } from "@tanstack/react-query"

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

/** DELETE /api/me — anonimizacja konta i usunięcie logowania. 409 "Conflict.Account" = jedyny organizator. */
export function useDeleteMyAccount() {
  const { request } = useApi()
  return useMutation({
    mutationFn: () => request("/api/me", { method: "DELETE" }),
    meta: { handlesConflict: true },
  })
}
