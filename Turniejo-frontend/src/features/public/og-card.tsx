import type { ReactNode } from "react"

import { SITE_URL } from "@/lib/site"

// Karta podglądu linku (Facebook, Messenger, WhatsApp, X). Renderuje ją next/og (Satori): tylko
// flexbox i style inline, czcionka domyślna next/og (Geist) ma polskie znaki.

export const OG_SIZE = { width: 1200, height: 630 }

const COLORS = {
  background: "#121417",
  card: "#1b1e23",
  border: "#2a2f36",
  text: "#f5f6f8",
  muted: "#9aa3ae",
  primary: "#10b981",
  live: "#f59e0b",
}

function TrophyIcon({ size }: { size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="#04130d"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M10 14.66v1.626a2 2 0 0 1-.976 1.696A5 5 0 0 0 7 21.978" />
      <path d="M14 14.66v1.626a2 2 0 0 0 .976 1.696A5 5 0 0 1 17 21.978" />
      <path d="M18 9h1.5a1 1 0 0 0 0-5H18" />
      <path d="M4 22h16" />
      <path d="M6 9a6 6 0 0 0 12 0V3a1 1 0 0 0-1-1H7a1 1 0 0 0-1 1z" />
      <path d="M6 9H4.5a1 1 0 0 1 0-5H6" />
    </svg>
  )
}

export function Brand({ size = 44 }: { size?: number }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: size * 0.35 }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          width: size,
          height: size,
          borderRadius: size * 0.22,
          background: COLORS.primary,
        }}
      >
        <TrophyIcon size={size * 0.6} />
      </div>
      <span style={{ fontSize: size * 0.7, color: COLORS.text, letterSpacing: -0.5 }}>
        Turniejo
      </span>
    </div>
  )
}

/** Plakietka statusu: bursztyn tylko dla „na żywo”, jak w aplikacji. */
export function Badge({ children, live = false }: { children: ReactNode; live?: boolean }) {
  const color = live ? COLORS.live : COLORS.primary
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        padding: "8px 20px",
        borderRadius: 999,
        border: `2px solid ${color}`,
        color,
        fontSize: 28,
      }}
    >
      {live && <div style={{ width: 14, height: 14, borderRadius: 999, background: color }} />}
      {children}
    </div>
  )
}

const host = SITE_URL.replace(/^https?:\/\//, "")

/** Rama karty: logo u góry, treść, adres strony na dole. */
export function OgCard({ badge, children }: { badge?: ReactNode; children: ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        height: "100%",
        padding: 64,
        background: COLORS.background,
        backgroundImage: `radial-gradient(circle at 85% 15%, rgba(16,185,129,0.18), transparent 55%)`,
        color: COLORS.text,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <Brand />
        {badge}
      </div>
      <div style={{ display: "flex", flexDirection: "column", flex: 1, justifyContent: "center" }}>
        {children}
      </div>
      <div style={{ display: "flex", fontSize: 26, color: COLORS.muted }}>{host}</div>
    </div>
  )
}

export const OG_COLORS = COLORS

/** Nazwa skrócona do długości, która mieści się w dwóch liniach przy danym rozmiarze. */
export function clip(text: string, max: number) {
  return text.length > max ? `${text.slice(0, max - 1).trimEnd()}…` : text
}
