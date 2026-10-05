import type { Metadata } from "next"
import Link from "next/link"
import { redirect } from "next/navigation"

import { auth } from "@/auth"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { DEFAULT_APP_PATH, safeCallbackUrl } from "@/lib/auth/public-paths"

import { LoginForm } from "./login-form"

export const metadata: Metadata = { title: "Logowanie" }

export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const callbackUrl = safeCallbackUrl((await searchParams).callbackUrl)

  const session = await auth()
  if (session && !session.error) redirect(callbackUrl)

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Zaloguj się</CardTitle>
        <CardDescription>Zarządzaj turniejami, drużynami i meczami</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
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
