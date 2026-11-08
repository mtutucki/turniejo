import type { Metadata } from "next"
import Link from "next/link"
import { redirect } from "next/navigation"

import { auth } from "@/auth"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { DEFAULT_APP_PATH, safeCallbackUrl } from "@/lib/auth/public-paths"

import { LoginForm } from "./login-form"

export const metadata: Metadata = { title: "Logowanie" }

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const params = await searchParams
  const callbackUrl = safeCallbackUrl(params.callbackUrl)
  // Po usunięciu konta signOut przeładowuje stronę, więc toast nie zdążył się pokazać (QA v3: KON-009).
  const accountDeleted = params.accountDeleted === "1"

  const session = await auth()
  if (session && !session.error) redirect(callbackUrl)

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Zaloguj się</CardTitle>
        <CardDescription>Zarządzaj turniejami, drużynami i meczami</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {accountDeleted && (
          <p role="status" className="bg-muted rounded-lg px-3 py-2 text-center text-sm">
            Konto zostało usunięte.
          </p>
        )}
        <LoginForm callbackUrl={callbackUrl} />
        <p className="text-muted-foreground text-center text-sm">
          Nie masz konta?{" "}
          <Link
            // Link z zaproszenia (/join?code=…) prowadzi przez logowanie — rejestracja zachowuje cel.
            href={
              callbackUrl === DEFAULT_APP_PATH
                ? "/register"
                : `/register?callbackUrl=${encodeURIComponent(callbackUrl)}`
            }
            className="text-primary font-medium hover:underline"
          >
            Zarejestruj się
          </Link>
        </p>
      </CardContent>
    </Card>
  )
}
