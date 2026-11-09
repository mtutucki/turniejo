# Turniejo: instrukcje dla agenta

Aplikacja do turniejów piłkarskich. Backend .NET (`Turniejo-backend/`), frontend Next.js (`Turniejo-frontend/`), wdrożenie w `deploy/`. Każda część ma własny `CLAUDE.md` z konwencjami - przeczytaj go, zanim zmienisz kod w tej części. Opis dla ludzi: [README.md](README.md), dokumenty w [docs/](docs).

## Jak pracujemy

Pełny opis: [docs/jak-pracujemy.md](docs/jak-pracujemy.md). Skrót:

1. Każda funkcja i poprawka na osobnym branchu od aktualnego `master`: `feat/…`, `fix/…`, `chore/…`.
2. Najpierw krótki plan i kryteria akceptacji (z issue na GitHubie albo od użytkownika). Niejasna decyzja produktowa: zapytaj, nie zgaduj. Zapisz ją w [docs/decyzje.md](docs/decyzje.md).
3. Każde kryterium ma test na właściwej warstwie ([docs/testy.md](docs/testy.md)). Poprawka błędu zaczyna się od testu, który ten błąd pokazuje.
4. Jeden commit na jedną zmianę (poprawkę, regułę, ekran), z opisem „dlaczego”. Nie mieszaj sprzątania z funkcją.
5. Przed oddaniem: `bash scripts/check.sh` (a przy zmianach UI także `--e2e`) musi przejść.
6. Nie pushuj i nie zakładaj PR bez prośby użytkownika. Opis PR po angielsku, z sekcjami Summary i Test plan.

Do prowadzenia funkcji od początku do końca służy skill [nowa-funkcja](.claude/skills/nowa-funkcja/SKILL.md).

## Twarde zasady

- Nigdy nie testuj na produkcji i nie łącz się z produkcyjną bazą ani Keycloakiem.
- Sekrety nie trafiają do repo: `appsettings.Development.json`, `.env*`, `deploy/.env` są ignorowane i takie zostają.
- Lokalny backend wysyła maile przez prawdziwy SMTP (Brevo w `appsettings.Development.json`). Nie wywołuj resetu hasła ani rejestracji na prawdziwe adresy. Konta testowe: `qa+…@test.pl`. Testy automatyczne używają fałszywej skrzynki albo Mailpita.
- Danych w lokalnej bazie (turnieje, konta) nie usuwaj bez zgody użytkownika. Haseł kont testowych nie zmieniaj.
- Zmiana `Turniejo-backend/infra/keycloak/turniejo-realm.json` nie działa na istniejących instancjach (import tylko przy pierwszym starcie). Zmianę trzeba też nanieść ręcznie w dev (`kcadm.sh`) i opisać dla produkcji.
- Lokalny stos dev uruchamiaj jako projekt `turniejo`: `docker compose -p turniejo …` (inaczej powstanie drugi, pusty wolumen bazy).

## Teksty w aplikacji (UI)

- Po polsku, krótko i naturalnie, jedno zdanie zamiast dwóch. Bez długich myślników „—”, używaj przecinka albo kropki.
- Bez technicznych szczegółów w komunikatach. Błędy z backendu (angielskie) tłumaczy `Turniejo-frontend/src/lib/api/messages.ts` - nowy komunikat backendu = nowe tłumaczenie.
- Użytkownik może mieć kilka ról w jednym turnieju (np. sędzia i trener). Widoki zależne od roli muszą obsłużyć każdą z nich.

## Komendy

```bash
bash scripts/check.sh                 # szybka kontrola całości
bash scripts/check.sh --e2e           # + E2E na mocku
cd Turniejo-frontend && npm run e2e:full:up && npm run test:e2e:full && npm run e2e:full:down   # pełny stos (Docker)
```

Testy backendu przy uruchomionym API blokują pliki w `bin/` - dodaj `--artifacts-path "$TEMP/turniejo-test"` albo zatrzymaj API.
