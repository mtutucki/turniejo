"use client"

import Link from "next/link"
import { useEffect, useRef, useState } from "react"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { useMarkReleaseSeen, useMyProfile } from "@/features/account/api"
import { plural } from "@/lib/format"

import { silentlySeen, whatsNewSince } from "./releases"

/**
 * Okno „Co nowego” po wydaniu nowej wersji. Jedno okno ze wszystkimi zmianami od ostatniej wizyty,
 * zamknięcie zapisuje wersję na koncie. Nowe konto i same poprawki: zapis bez okna (docs/decyzje.md).
 */
export function WhatsNewDialog() {
  const { data: profile } = useMyProfile()
  const markSeen = useMarkReleaseSeen()
  const [closed, setClosed] = useState(false)
  const savedSilently = useRef(false)

  const seen = profile?.seenReleaseVersion ?? null
  const news = profile ? whatsNewSince(seen) : null
  const silent = profile ? silentlySeen(seen) : null

  const { mutate } = markSeen
  useEffect(() => {
    if (!silent || savedSilently.current) return
    savedSilently.current = true
    mutate(silent)
  }, [silent, mutate])

  if (!news) return null

  const close = () => {
    if (closed) return
    setClosed(true)
    mutate(news.latest)
  }

  return (
    <Dialog open={!closed} onOpenChange={(open) => !open && close()}>
      <DialogContent className="max-h-[85dvh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Co nowego w Turniejo</DialogTitle>
          <DialogDescription>
            {news.versions.length === 1
              ? `Wersja ${news.latest}`
              : "Zmiany od Twojej ostatniej wizyty"}
          </DialogDescription>
        </DialogHeader>
        <ul className="list-disc space-y-2 pl-5 text-sm">
          {news.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <p className="text-muted-foreground text-sm">
          {news.hidden > 0 && `I jeszcze ${plural(news.hidden, ["zmiana", "zmiany", "zmian"])}. `}
          <Link href="/changelog" onClick={close} className="text-primary font-medium hover:underline">
            Zobacz wszystkie zmiany
          </Link>
        </p>
        <DialogFooter>
          <Button onClick={close}>Rozumiem</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
