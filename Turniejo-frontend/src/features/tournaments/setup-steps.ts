// Bez "use client": listy kroków czyta też strona serwerowa kreatora (walidacja ?step=), a wartość
// zaimportowana z modułu klienckiego jest po stronie serwera tylko referencją, nie tablicą.

/** Kroki kreatora w kolejności, w jakiej jeden zależy od drugiego. */
export const SETUP_STEP_KEYS = [
  "format",
  "rules",
  "portal",
  "categories",
  "pitches",
  "teams",
  "schedule",
] as const
export type SetupStepKey = (typeof SETUP_STEP_KEYS)[number]

/** Kroki, których ukończenie widać w danych turnieju (zasady gry i portal mają sensowne domyślne). */
export const SETUP_CHECKLIST = ["format", "categories", "pitches", "teams", "schedule"] as const
export type SetupChecklistKey = (typeof SETUP_CHECKLIST)[number]
