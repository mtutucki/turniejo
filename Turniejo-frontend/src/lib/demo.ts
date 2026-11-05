/**
 * Instancja demonstracyjna (publiczne demo na VPS). Wpiekane w `next build` jak każda zmienna
 * NEXT_PUBLIC_*, więc zmiana wymaga przebudowania obrazu.
 */
export const IS_DEMO = process.env.NEXT_PUBLIC_DEMO_MODE === "true"
