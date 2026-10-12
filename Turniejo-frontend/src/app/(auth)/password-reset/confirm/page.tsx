import type { Metadata } from "next"
import Link from "next/link"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

import { NewPasswordForm } from "./new-password-form"

export const metadata: Metadata = {
  title: "Nowe hasło",
  // Adres zawiera jednorazowy token — nie może wyciec w nagłówku Referer.
  referrer: "no-referrer",
}

export default async function PasswordResetConfirmPage({
  searchParams,
}: PageProps<"/password-reset/confirm">) {
  const { token } = await searchParams
  const resetToken = Array.isArray(token) ? token[0] : token

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Nowe hasło</CardTitle>
        <CardDescription>
          {resetToken ? "Ustaw hasło, którym będziesz się logować" : "Ten link jest niepełny"}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {resetToken ? (
          <NewPasswordForm token={resetToken} />
        ) : (
          <p className="text-center text-sm">
            Otwórz link z wiadomości email jeszcze raz albo{" "}
            <Link href="/password-reset" className="text-primary font-medium hover:underline">
              poproś o nowy
            </Link>
            .
          </p>
        )}
        <p className="text-center text-sm">
          <Link href="/login" className="text-muted-foreground hover:text-foreground">
            Wróć do logowania
          </Link>
        </p>
      </CardContent>
    </Card>
  )
}
