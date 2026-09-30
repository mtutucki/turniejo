import type { FieldValues, Path, UseFormReturn } from "react-hook-form"

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
 * Rozkłada błędy walidacji (400/422) na pola formularza React Hook Form. Błąd pola, którego
 * formularz nie ma (np. "Status" przy edycji zakończonego turnieju), trafia do `root`.
 * Zwraca true, jeśli błąd został obsłużony (mutacja powinna mieć meta.handlesValidation).
 */
export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  form: Pick<UseFormReturn<T>, "setError" | "getValues">,
): boolean {
  if (!isApiError(error) || ![400, 422].includes(error.status) || error.fieldErrors.length === 0)
    return false

  const knownFields = new Set(Object.keys(form.getValues()))
  for (const { field, message } of error.fieldErrors) {
    const path = field ? toFieldPath(field) : ""
    const isKnown = path !== "" && knownFields.has(path.split(".")[0]!)
    form.setError((isKnown ? path : "root") as Path<T>, { type: "server", message })
  }
  return true
}
