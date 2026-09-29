"use client"

import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useCurrentUser } from "@/hooks/use-current-user"
import { useMyRoleAssignments } from "@/hooks/use-my-roles"

export function AccountOverview() {
  const { data: user, isLoading: userLoading } = useCurrentUser()
  const { data: roles, isLoading: rolesLoading } = useMyRoleAssignments()

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Konto</CardTitle>
          <CardDescription>Dane z API (GET /api/me)</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {userLoading ? (
            <>
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-4 w-64" />
            </>
          ) : user ? (
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
              <dt className="text-muted-foreground">Email</dt>
              <dd className="truncate">{user.email}</dd>
              <dt className="text-muted-foreground">ID</dt>
              <dd className="truncate font-mono text-xs leading-5">{user.userId}</dd>
            </dl>
          ) : (
            <p className="text-muted-foreground">Nie udało się pobrać danych konta.</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Role</CardTitle>
          <CardDescription>Uprawnienia przypisane per zasób</CardDescription>
        </CardHeader>
        <CardContent className="text-sm">
          {rolesLoading ? (
            <Skeleton className="h-4 w-40" />
          ) : roles && roles.length > 0 ? (
            <ul className="space-y-2">
              {roles.map((r) => (
                <li
                  key={`${r.role}-${r.scopeType}-${r.scopeId}`}
                  className="flex items-center gap-2"
                >
                  <Badge>{r.role}</Badge>
                  <span className="text-muted-foreground">{r.scopeType}</span>
                  <span className="text-muted-foreground truncate font-mono text-xs">
                    {r.scopeId}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground">
              Brak przypisanych ról. Utwórz turniej albo dołącz do istniejącego kodem.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
