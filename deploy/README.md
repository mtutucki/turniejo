# Deploy na VPS

Jeden serwer, wszystko w Dockerze. Na zewnątrz wystawiony jest tylko Caddy (80/443, automatyczne
certyfikaty Let's Encrypt).

```
Internet ─443─> Caddy ─┬─> frontend (Next.js)  https://DOMAIN        ─/backend/*─> api
                       ├─> api (tylko huby SignalR: /backend/hubs/*)
                       └─> keycloak            https://auth.DOMAIN   (bez /admin i konsoli konta)
                 sieć wewnętrzna: postgres (osobne bazy/userzy dla aplikacji i Keycloaka)
```

## Pierwsze wdrożenie

1. **DNS**: rekordy A (i AAAA, jeśli jest IPv6) dla `DOMAIN` i `auth.DOMAIN` wskazują na VPS.
2. **Serwer**: Docker + plugin compose, firewall z otwartymi tylko 22/80/443.
3. **Kod i sekrety**:
   ```sh
   git clone <repo> turniejo && cd turniejo/deploy
   cp .env.example .env && chmod 600 .env
   # uzupełnij .env — sekrety: openssl rand -hex 32
   ```
4. **Start**:
   ```sh
   docker compose -f docker-compose.prod.yml up -d --build
   ```
   Przy pierwszym starcie Postgres tworzy bazy i userów (`postgres/init.sh`), Keycloak importuje
   realm `turniejo`, a API nakłada migracje wszystkich modułów (`Database__MigrateOnStartup`).
5. **SMTP w Keycloaku** (bez tego nie działa reset hasła): konsola admina → realm `turniejo` →
   Realm settings → Email. Dane SMTP to sekret, więc nie trafiają do `turniejo-realm.json`.
6. **Sprawdzenie**: `https://DOMAIN/backend/health` powinno zwrócić `Healthy` (sprawdza też bazę).
   Ten adres podepnij do monitoringu uptime.

## Konsola admina Keycloaka

Nie jest wystawiona publicznie (Caddy zwraca 404 na `/admin`). Dostęp przez tunel SSH:

```sh
ssh -L 8081:127.0.0.1:8081 user@vps
# potem w przeglądarce: http://localhost:8081/admin
```

## Aktualizacja

```sh
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

Migracje nakładają się same przy starcie API.

**Uwaga — realm importuje się tylko raz.** `--import-realm` pomija realm, który już istnieje, więc
późniejsze zmiany w `Turniejo-backend/infra/keycloak/turniejo-realm.json` trzeba wprowadzić ręcznie
w konsoli admina (albo przez `kcadm.sh`). Tak samo zmiana `KEYCLOAK_BACKEND_ADMIN_SECRET` w `.env`
wymaga ustawienia nowego sekretu klienta `turniejo-backend-admin` w konsoli.

Analogicznie hasła do bazy z `.env` są ustawiane tylko przy pierwszym starcie z pustym wolumenem —
późniejsza zmiana wymaga `ALTER ROLE ... PASSWORD` w Postgresie.

## Backup

Minimum: codzienny dump obu baz poza serwer, np. z crona:

```sh
docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U postgres -Fc turniejo > turniejo-$(date +%F).dump
docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U postgres -Fc keycloak > keycloak-$(date +%F).dump
```

Raz przetestuj odtworzenie (`pg_restore`) na czystej bazie.
