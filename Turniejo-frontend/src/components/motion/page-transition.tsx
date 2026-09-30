"use client"

import { motion } from "motion/react"
import type { ReactNode } from "react"

/**
 * Wejście treści strony (template.tsx remontuje je przy każdej nawigacji). Krótko i bez przesunięć
 * layoutu — przy "reduced motion" MotionConfig w Providers wyłącza ruch, zostaje sama zmiana.
 */
export function PageTransition({ children }: { children: ReactNode }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.18, ease: "easeOut" }}
    >
      {children}
    </motion.div>
  )
}
