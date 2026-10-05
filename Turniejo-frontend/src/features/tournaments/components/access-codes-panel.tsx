"use client"

import { Check, Copy, Link2 } from "lucide-react"
import { useState } from "react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import type { Tournament } from "@/types/tournaments"

const CODES = [
  { key: "refereeCode", label: "Sędzia" },
  { key: "coachCode", label: "Trener" },
  { key: "viewerCode", label: "Kibic / rodzic" },
] as const

function CodeRow({ label, code }: { label: string; code: string }) {
  const [copied, setCopied] = useState<"code" | "link" | null>(null)

  // Link zaproszenia: bez konta prowadzi przez rejestrację (typ "Dołączam do turnieju"),
  // a po zalogowaniu od razu dołącza z tym kodem.
  async function copy(kind: "code" | "link") {
    const text = kind === "code" ? code : `${window.location.origin}/join?code=${code}`
    try {
      await navigator.clipboard.writeText(text)
      setCopied(kind)
      toast.success(
        kind === "code" ? `Skopiowano kod: ${label}` : `Skopiowano link zaproszenia: ${label}`,
      )
      setTimeout(() => setCopied(null), 1500)
    } catch {
      toast.error("Nie udało się skopiować — zaznacz kod ręcznie")
    }
  }

  return (
    <li className="flex items-center justify-between gap-3 py-2">
      <span className="text-muted-foreground text-sm">{label}</span>
      <span className="flex items-center gap-1">
        <code className="bg-secondary rounded-md px-2 py-1 font-mono text-sm tracking-widest select-all">
          {code}
        </code>
        <Button
          variant="ghost"
          size="icon"
          className="size-8"
          onClick={() => copy("code")}
          aria-label={`Kopiuj kod: ${label}`}
          title="Kopiuj kod"
        >
          {copied === "code" ? <Check className="text-primary" /> : <Copy />}
        </Button>
        <Button
          variant="ghost"
          size="icon"
          className="size-8"
          onClick={() => copy("link")}
          aria-label={`Kopiuj link zaproszenia: ${label}`}
          title="Kopiuj link zaproszenia"
        >
          {copied === "link" ? <Check className="text-primary" /> : <Link2 />}
        </Button>
      </span>
    </li>
  )
}

/** Kody, którymi inni dołączają do turnieju (/join). Pokazywać tylko organizatorowi. */
export function AccessCodesPanel({ tournament }: { tournament: Tournament }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Kody dostępu</CardTitle>
        <CardDescription>
          Wyślij kod albo link zaproszenia osobie, która ma dołączyć w danej roli — link poprowadzi
          ją też przez założenie konta.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="divide-y">
          {CODES.map(({ key, label }) => {
            const code = tournament[key]
            return code ? <CodeRow key={key} label={label} code={code} /> : null
          })}
        </ul>
      </CardContent>
    </Card>
  )
}
