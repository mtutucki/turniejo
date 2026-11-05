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
5. **SMTP** (bez tego nie działa reset hasła): zmienne `SMTP_*` w `.env`. Maile wysyła API, a link
   z maila prowadzi na `https://DOMAIN/password-reset/confirm` — Keycloak nie wysyła żadnych maili.
6. **Sprawdzenie**: `https://DOMAIN/backend/health` powinno zwrócić `Healthy` (sprawdza też bazę).
   Ten adres podepnij do monitoringu uptime.

## Konsola admina Keycloaka

Nie jest wystawiona publicznie (Caddy zwraca 404 na `/admin`). Dostęp przez tunel SSH:

```sh
ssh -L 8081:127.0.0.1:8081 user@vps
# potem w przeglądarce: http://localhost:8081/admin
```

## pgAdmin (podgląd bazy)

Postgres słucha tylko na `127.0.0.1:5432` serwera, więc z internetu go nie widać. pgAdmin na
własnym komputerze łączy się przez tunel SSH:

- **Connection**: host `127.0.0.1`, port `5432`, user `turniejo_app` (hasło `APP_DB_PASSWORD`) do
  przeglądania danych aplikacji albo `postgres` (`POSTGRES_PASSWORD`) do administracji.
- **SSH Tunnel**: włączony, host = IP serwera, user `deploy`, Identity file = prywatny klucz SSH.

Bez pgAdmina: `ssh -L 5432:127.0.0.1:5432 deploy@IP_SERWERA` i dowolny klient na `localhost:5432`
(jeśli lokalnie działa już Postgres na 5432, użyj np. `-L 5433:127.0.0.1:5432`).

## Aktualizacja

`deploy.sh` robi backup baz, ustawia kod na `origin/master`, przebudowuje kontenery i czeka, aż
`/backend/health` odpowie. Ręcznie na serwerze:

```sh
~/turniejo/deploy/deploy.sh
```

Automatycznie: `.github/workflows/deploy.yml` uruchamia go po każdym pushu na `master` (albo ręcznie
z zakładki Actions). Workflow loguje się kluczem z sekretów repo (`VPS_HOST`, `VPS_USER`,
`VPS_SSH_KEY`, `VPS_KNOWN_HOSTS`). Na serwerze ten klucz jest w `~/.ssh/authorized_keys` z
ograniczeniem, więc może tylko uruchomić wdrożenie:

```
command="/home/deploy/turniejo/deploy/deploy.sh",restrict ssh-ed25519 AAAA... github-actions
```

Migracje nakładają się same przy starcie API.

**Uwaga — realm importuje się tylko raz.** `--import-realm` pomija realm, który już istnieje, więc
późniejsze zmiany w `Turniejo-backend/infra/keycloak/turniejo-realm.json` trzeba wprowadzić ręcznie
w konsoli admina (albo przez `kcadm.sh`). Tak samo zmiana `KEYCLOAK_BACKEND_ADMIN_SECRET` w `.env`
wymaga ustawienia nowego sekretu klienta `turniejo-backend-admin` w konsoli.

Analogicznie hasła do bazy z `.env` są ustawiane tylko przy pierwszym starcie z pustym wolumenem —
późniejsza zmiana wymaga `ALTER ROLE ... PASSWORD` w Postgresie.

## Backup

`backup.sh` zrzuca obie bazy do `~/backups` i usuwa zrzuty starsze niż 14 dni. Uruchamia go
`deploy.sh` przed każdym wdrożeniem, a codziennie cron (`crontab -e`):

```
30 3 * * * /home/deploy/turniejo/deploy/backup.sh >> /home/deploy/backups/cron.log 2>&1
```

Te pliki leżą na tym samym serwerze, więc włącz też kopie zapasowe/snapshoty u dostawcy VPS.
Odtworzenie bazy z pliku (przykład dla aplikacji):

```sh
docker compose -f docker-compose.prod.yml exec -T postgres   pg_restore -U postgres -d turniejo --clean --if-exists < ~/backups/turniejo-RRRR-MM-DD_GGMM.dump
```

Raz przetestuj odtworzenie na czystej bazie.
