import Image from "next/image"

import { cn } from "@/lib/utils"

type TeamLogoProps = {
  name: string
  logoUrl: string | null
  size?: number
  className?: string
}

const isOptimizable = (url: string) => url.startsWith("https://") && !/\.svg(\?|#|$)/i.test(url)

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
        // https → optymalizator Next.js (next.config: remotePatterns). SVG i http idą bez niego:
        // SVG optymalizator odrzuca (dangerouslyAllowSVG=false), a http nie jest na liście wzorców.
        <Image
          src={logoUrl}
          alt={`Herb: ${name}`}
          fill
          sizes={`${size}px`}
          unoptimized={!isOptimizable(logoUrl)}
          className="object-contain"
        />
      ) : (
        initials
      )}
    </span>
  )
}
