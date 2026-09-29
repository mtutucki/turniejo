import type { FieldValues, Path, UseFormSetError } from "react-hook-form"

import { isApiError } from "./errors"

// FluentValidation zwraca nazwy właściwości C# ("Settings.MatchDuration"),
// formularze używają camelCase ("settings.matchDuration").
function toFieldPath(field: string) {
  return field
    .replace(/^\$\./, "")
    .split(".")
    .map((part) => part.charAt(0).toLowerCase() + part.slice(1))
    .join(".")
}

/**
 * Rozkłada błędy walidacji (400/422) na pola formularza React Hook Form.
 * Zwraca true, jeśli błąd został obsłużony (mutacja powinna mieć meta.handlesValidation).
 */
export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
): boolean {
  if (!isApiError(error) || ![400, 422].includes(error.status) || error.fieldErrors.length === 0)
    return false

  for (const { field, message } of error.fieldErrors) {
    const path = field ? toFieldPath(field) : "root"
    setError(path as Path<T>, { type: "server", message })
  }
  return true
}
