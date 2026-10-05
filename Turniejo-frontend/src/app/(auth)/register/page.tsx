import type { Metadata } from "next"
import Link from "next/link"
import { redirect } from "next/navigation"

import { auth } from "@/auth"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { DEFAULT_APP_PATH, safeCallbackUrl } from "@/lib/auth/public-paths"

import { RegisterForm } from "./register-form"

export const metadata: Metadata = { title: "Rejestracja" }

export default async function RegisterPage({ searchParams }: PageProps<"/register">) {
  const callbackUrl = safeCallbackUrl((await searchParams).callbackUrl)

  const session = await auth()
  if (session && !session.error) redirect(callbackUrl)

  // Wejście z linku zaproszenia (/join?code=…) — konto uczestnika, po rejestracji od razu dołącza.
  const invited = callbackUrl.startsWith("/join")

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Załóż konto</CardTitle>
        <CardDescription>
          {invited
            ? "Po założeniu konta od razu dołączysz do turnieju"
            : "Organizuj turnieje albo dołącz do istniejących"}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <RegisterForm
          callbackUrl={callbackUrl === DEFAULT_APP_PATH ? null : callbackUrl}
          defaultAccountType={invited ? "participant" : undefined}
        />
        <p className="text-muted-foreground text-center text-sm">
          Masz już konto?{" "}
          <Link
            href={
              callbackUrl === DEFAULT_APP_PATH
                ? "/login"
                : `/login?callbackUrl=${encodeURIComponent(callbackUrl)}`
            }
            className="text-foreground font-medium hover:underline"
          >
            Zaloguj się
          </Link>
        </p>
      </CardContent>
    </Card>
  )
}
