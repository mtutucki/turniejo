import Link from "next/link"

import { cn } from "@/lib/utils"

const LINKS = [
  { href: "/help", label: "Pomoc" },
  { href: "/changelog", label: "Co nowego" },
  { href: "/terms", label: "Regulamin" },
  { href: "/privacy", label: "Polityka prywatności" },
]

/** Stopka z dokumentami — ekran logowania, strony publiczne, portal kibica, ustawienia konta. */
export function LegalLinks({
  className,
  withHelp = true,
}: {
  className?: string
  /** false tam, gdzie obok jest już osobny link do pomocy (ekran logowania). */
  withHelp?: boolean
}) {
  const links = withHelp ? LINKS : LINKS.filter(({ href }) => href !== "/help")
  return (
    <nav
      aria-label="Informacje"
      className={cn("text-muted-foreground flex flex-wrap gap-x-4 gap-y-1 text-xs", className)}
    >
      {links.map(({ href, label }) => (
        <Link key={href} href={href} className="hover:text-foreground hover:underline">
          {label}
        </Link>
      ))}
    </nav>
  )
}
