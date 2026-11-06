import Image from "next/image"

import { cn } from "@/lib/utils"

type TeamLogoProps = {
  name: string
  logoUrl: string | null
  size?: number
  className?: string
}

/** Herb drużyny albo inicjały, gdy logo nie ustawiono. */
export function TeamLogo({ name, logoUrl, size = 36, className }: TeamLogoProps) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0]!.toUpperCase())
    .join("")

  return (
    <span
      className={cn(
        "bg-secondary text-muted-foreground relative flex shrink-0 items-center justify-center overflow-hidden rounded-md text-xs font-semibold",
        className,
      )}
      style={{ width: size, height: size }}
    >
      {logoUrl ? (
        // Bez optymalizatora Next.js: wgrane herby mają już 256 px, a obrazki z linków przeglądarka
        // ładuje sama, zamiast żeby nasz serwer pobierał dowolne adresy (next.config.ts).
        <Image
          src={logoUrl}
          alt={`Herb: ${name}`}
          fill
          sizes={`${size}px`}
          unoptimized
          className="object-contain"
        />
      ) : (
        initials
      )}
    </span>
  )
}
