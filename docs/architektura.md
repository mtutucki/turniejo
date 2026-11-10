# Architektura

## Całość

```
przeglądarka ──► frontend (Next.js) ──/backend/* (rewrite)──► API (.NET) ──► PostgreSQL (schemat na moduł)
      │                │                                         │
      │                └──── logowanie (Auth.js) ──► Keycloak ◄──┘ (konta, Admin API)
      └──── SignalR /backend/hubs/* (wyniki na żywo) ───────────►│
                                                                  └──► SMTP (maile: reset hasła)
```

Na produkcji przed wszystkim stoi Caddy (HTTPS): `TWOJA-DOMENA` → frontend, `auth.TWOJA-DOMENA` → Keycloak. Szczegóły: [../deploy/README.md](../deploy/README.md).

## Backend: moduły

Modularny monolit: jeden proces, ale każdy moduł ma osobne projekty (Domain / Application / Infrastructure), własny schemat w bazie i własne migracje. Moduły rozmawiają tylko przez interfejsy z `SharedKernel/Contracts` (synchronicznie) albo zdarzenia w outboxie (asynchronicznie).

| Moduł | Odpowiada za |
|---|---|
| Users | konta (Id = `sub` z Keycloaka), role w zakresach |
| Auth | integracja z Keycloakiem (JWT, Admin API) |
| Organizations | organizacje (klub, szkoła) właściciela turniejów |
| Tournaments | turnieje, etapy, kategorie, grupy, ustawienia, portal, kody dołączenia |
| Teams | drużyny organizacji, zgłoszenia do turnieju, sztab |
| Players | zawodnicy, kadra drużyny, składy turniejowe, opiekunowie |
| Matches | boiska, terminarz, mecze na żywo, zdarzenia, statystyki, huby SignalR |
| Standings | tabele grup (projekcja z meczów) |
| Brackets | drabinka pucharowa, awans zwycięzców; awans z tabel do grup drugiej fazy (miejsca `SecondStageSeat` i zaplanowane mecze `SecondStageFixture`) |
| Notifications | powiadomienia w aplikacji i przypomnienia o meczach |

## Przepływ danych

- **Źródło prawdy:** zdarzenia meczu (`MatchEvent`). Wynik meczu, tabela grupy i drabinka to projekcje przeliczane po zmianie (gol, cofnięcie, korekta, zakończenie).
- **Outbox:** handler zapisuje zdarzenie domenowe razem ze zmianą w tej samej transakcji. `OutboxProcessor` (usługa w tle w API) publikuje je przez MediatR do handlerów w innych modułach, np. mecz zakończony → przeliczenie tabeli → uzupełnienie drabinki (albo miejsc w grupach drugiej fazy) → powiadomienia.
- **Usługi w tle:** `OutboxProcessor` i `MatchReminderBackgroundService` (przypomnienia przed meczem).

## Uprawnienia

- Logowanie: Keycloak, token JWT. Backend nie przechowuje haseł.
- Role w zakresach (`RoleScopeType`): organizator w Organization i Tournament, sędzia w Tournament (pula meczów) i Match (prowadzony mecz), trener w TournamentTeam, rodzic przez opiekę nad zawodnikiem (Player), kibic w Tournament.
- Role nadaje się kodem turnieju (sędzia, trener, kibic) albo przez organizatora (trener drużyny, sędzia meczu, opiekun zawodnika).
- Sprawdzanie: `AuthorizationBehavior` w potoku MediatR na podstawie oznaczenia command/query (szczegóły w [../Turniejo-backend/CLAUDE.md](../Turniejo-backend/CLAUDE.md)). Brak uprawnień = 403.

## Czas rzeczywisty

SignalR: `/hubs/matches` (panel meczu, wynik na żywo), `/hubs/tournaments` (terminarz, drabinka, listy sędziów), `/hubs/notifications`. Frontend łączy się przez rewrite `/backend/hubs/*`, token w query stringu. Portal kibica łączy się bez logowania do publicznych turniejów.

## Frontend

- Panel (`app/(app)`) i panel meczu (`app/(live)`) to aplikacja kliencka z React Query.
- Portal kibica (`app/t/[slug]`) renderuje się na serwerze (SSR) i odświeża na żywo przez SignalR.
- Logowanie: Auth.js z formularzem w aplikacji (hasło przez Keycloak), sesja odświeżana co 30 s.
