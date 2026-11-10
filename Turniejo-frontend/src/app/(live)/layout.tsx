import type { Metadata } from "next"

// Panel meczu sędziego: nic do znalezienia w Google, a linki z niego nie są dla kibiców.
export const metadata: Metadata = { robots: { index: false, follow: false } }

// Panel live sędziego: pełny ekran telefonu, bez sidebara i dolnej nawigacji — każdy piksel
// idzie na wynik, zegar i duże przyciski pod kciuk.
export default function LiveLayout({ children }: LayoutProps<"/">) {
  return <div className="bg-background flex min-h-dvh flex-1 flex-col">{children}</div>
}
