import type { Metadata } from "next"
import Link from "next/link"
import { redirect } from "next/navigation"

import { auth, signIn } from "@/auth"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

export const metadata: Metadata = { title: "Logowanie" }

// Auth.js przekazuje pełny URL aplikacji — zostawiamy samą ścieżkę, żeby ?callbackUrl=
// nie dało się użyć jako open redirect na obcą domenę.
function safeCallbackUrl(value: string | string[] | undefined) {
  const url = Array.isArray(value) ? value[0] : value
  if (!url) return "/dashboard"
  try {
    const parsed = new URL(url, "http://localhost")
    if (parsed.pathname.startsWith("/login")) return "/dashboard"
    return `${parsed.pathname}${parsed.search}`
  } catch {
    return "/dashboard"
  }
}

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
        <form
          action={async () => {
            "use server"
            await signIn("keycloak", { redirectTo: callbackUrl })
          }}
        >
          <Button type="submit" size="lg" className="w-full">
            Zaloguj się
          </Button>
        </form>
        <div className="flex flex-col items-center gap-2 text-sm">
          <Link href="/password-reset" className="text-muted-foreground hover:text-foreground">
            Nie pamiętasz hasła?
          </Link>
          <p className="text-muted-foreground">
            Nie masz konta?{" "}
            <Link href="/register" className="text-foreground font-medium hover:underline">
              Zarejestruj się
            </Link>
          </p>
        </div>
      </CardContent>
    </Card>
  )
}
