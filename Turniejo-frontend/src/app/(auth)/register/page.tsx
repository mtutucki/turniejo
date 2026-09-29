import type { Metadata } from "next"
import Link from "next/link"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"

import { RegisterForm } from "./register-form"

export const metadata: Metadata = { title: "Rejestracja" }

export default function RegisterPage() {
  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Załóż konto</CardTitle>
        <CardDescription>Organizuj turnieje i dołączaj do istniejących</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <RegisterForm />
        <p className="text-muted-foreground text-center text-sm">
          Masz już konto?{" "}
          <Link href="/login" className="text-foreground font-medium hover:underline">
            Zaloguj się
          </Link>
        </p>
      </CardContent>
    </Card>
  )
}
