"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { PartyPopper } from "lucide-react"
import Link from "next/link"
import { useEffect, useRef } from "react"
import { useForm } from "react-hook-form"
import { z } from "zod"

import { PageHeader } from "@/components/layout/page-header"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { useJoinTournament } from "@/features/teams/api"
import { useApi } from "@/hooks/use-api"
import { applyFieldErrors } from "@/lib/api/form-errors"
import { useActiveTournamentStore } from "@/stores/active-tournament-store"
import { JOIN_ROLE_LABELS, type JoinResult } from "@/types/teams"

// Kody generuje Tournament.Create: 6 znaków szesnastkowych, wielkie litery.
const schema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^[0-9A-F]{6}$/, "Kod ma 6 znaków: cyfry i litery A–F"),
})

type JoinValues = z.infer<typeof schema>

/** Co dalej po dołączeniu — zależy od roli, którą dał kod. */
function NextSteps({ result }: { result: JoinResult }) {
  const { tournamentId, slug, role, isPublic } = result
  if (role === "Referee") {
    return (
      <>
        <p className="text-muted-foreground text-sm">
          Mecze, które organizator Ci przydzieli, znajdziesz w zakładce „Sędziowanie”.
        </p>
        <div className="flex flex-wrap justify-center gap-2">
          <Button asChild>
            <Link href="/referee/matches">Moje mecze</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/tournaments/${tournamentId}/dashboard`}>Przejdź do turnieju</Link>
          </Button>
        </div>
      </>
    )
  }
  if (role === "Coach") {
    return (
      <>
        <p className="text-muted-foreground text-sm">
          Organizator przypisze Cię teraz do drużyny — pojawi się w menu „Moje drużyny”.
        </p>
        <Button asChild>
          <Link href={`/tournaments/${tournamentId}/dashboard`}>Przejdź do turnieju</Link>
        </Button>
      </>
    )
  }
  return (
    <>
      <p className="text-muted-foreground text-sm">
        Jesteś rodzicem? Organizator przypisze Cię do dziecka — wtedy w menu pojawi się „Moje
        dzieci” z terminarzem i wynikami.{" "}
        {isPublic
          ? "Do tego czasu śledź turniej na portalu."
          : "Organizator nie udostępnił jeszcze portalu kibica — link pojawi się na stronie startowej, gdy to zrobi."}
      </p>
      {isPublic && (
        <Button asChild>
          <Link href={`/t/${slug}`}>Portal turnieju</Link>
        </Button>
      )}
    </>
  )
}

export function JoinTournament({ initialCode = "" }: { initialCode?: string }) {
  const join = useJoinTournament()
  const { isAuthenticated } = useApi()
  const setActiveTournament = useActiveTournamentStore((state) => state.setTournament)
  const form = useForm<JoinValues>({
    resolver: zodResolver(schema),
    defaultValues: { code: initialCode },
  })
  const { errors } = form.formState
  const codeField = form.register("code")

  function submit({ code }: JoinValues) {
    join.mutate(code, {
      onSuccess: (result) =>
        setActiveTournament({ id: result.tournamentId, name: result.tournamentName }),
      onError: (error) => applyFieldErrors(error, form),
    })
  }

  // Link zaproszenia (/join?code=…) dołącza od razu — raz na wejście, bez ponownego wpisywania kodu.
  // Czekamy na sesję: przy pierwszym renderze tokenu jeszcze nie ma, a zapytanie bez niego kończyło
  // się 403 i komunikatem "Brak uprawnień", mimo że kod był dobry.
  const autoJoined = useRef(false)
  useEffect(() => {
    if (!initialCode || !isAuthenticated || autoJoined.current) return
    autoJoined.current = true
    void form.handleSubmit(submit)()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- raz, gdy sesja jest gotowa
  }, [initialCode, isAuthenticated])

  if (join.isSuccess) {
    const { tournamentName, role } = join.data
    return (
      <>
        <PageHeader title="Dołącz do turnieju" />
        <Card className="max-w-md">
          <CardContent className="flex flex-col items-center gap-4 py-4 text-center">
            <PartyPopper className="text-primary size-10" />
            <p className="text-lg">
              Dołączyłeś jako{" "}
              <span className="text-primary font-semibold">{JOIN_ROLE_LABELS[role] ?? role}</span>{" "}
              do turnieju <span className="font-semibold">{tournamentName}</span>
            </p>
            <NextSteps result={join.data} />
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                join.reset()
                form.reset({ code: "" })
              }}
            >
              Użyj innego kodu
            </Button>
          </CardContent>
        </Card>
      </>
    )
  }

  return (
    <>
      <PageHeader title="Dołącz do turnieju" />
      <Card className="max-w-md">
        <CardHeader>
          <CardTitle className="text-base">Kod dostępu</CardTitle>
          <CardDescription>
            Kod dostaniesz od organizatora — określa, czy dołączysz jako sędzia, trener czy kibic.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={form.handleSubmit(submit)} noValidate>
            <FieldGroup>
              <Field data-invalid={!!errors.code}>
                <FieldLabel htmlFor="join-code">Kod</FieldLabel>
                <Input
                  id="join-code"
                  autoComplete="off"
                  autoCapitalize="characters"
                  spellCheck={false}
                  maxLength={6}
                  placeholder="np. 3F9A1C"
                  aria-invalid={!!errors.code}
                  className="h-12 text-center font-mono text-2xl tracking-[0.4em] uppercase"
                  {...codeField}
                  // Kod zawsze wielkimi literami — tak porównuje go backend.
                  onChange={(e) => {
                    e.target.value = e.target.value.toUpperCase().replace(/\s/g, "")
                    void codeField.onChange(e)
                  }}
                />
                <FieldDescription>6 znaków, wielkość liter nie ma znaczenia.</FieldDescription>
                <FieldError errors={[errors.code, errors.root]} />
              </Field>
              <Button type="submit" size="lg" disabled={join.isPending}>
                {join.isPending ? "Dołączanie…" : "Dołącz"}
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </>
  )
}
