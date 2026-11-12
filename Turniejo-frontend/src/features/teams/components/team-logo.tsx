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
      // Inicjały tylko wizualnie (CSS): nazwa stoi obok, więc nie powtarzamy jej w tekście strony
      // ani czytnikowi ekranu ("OE Orły E2E").
      data-initials={logoUrl ? undefined : initials}
      // Treść z CSS też trafia do nazwy dostępnej, więc inicjały ukrywamy przed czytnikiem.
      aria-hidden={logoUrl ? undefined : true}
      className={cn(
        "bg-secondary text-muted-foreground relative flex shrink-0 items-center justify-center overflow-hidden rounded-md text-xs font-semibold",
        !logoUrl && "before:content-[attr(data-initials)]",
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
      ) : null}
    </span>
  )
}
