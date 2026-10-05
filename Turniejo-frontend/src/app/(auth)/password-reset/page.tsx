import type { Metadata } from "next"
import Link from "next/link"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

import { PasswordResetForm } from "./password-reset-form"

export const metadata: Metadata = { title: "Reset hasła" }

export default function PasswordResetPage() {
  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Reset hasła</CardTitle>
        <CardDescription>Wyślemy link do ustawienia nowego hasła</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PasswordResetForm />
        <p className="text-center text-sm">
          <Link href="/login" className="text-muted-foreground hover:text-foreground">
            Wróć do logowania
          </Link>
        </p>
      </CardContent>
    </Card>
  )
}
