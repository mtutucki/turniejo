# Wersjonowanie

## Numer wersji: X.Y.Z

Trzy liczby, np. **1.4.2**. Którą podnosisz, zależy od tego, co zobaczy użytkownik od ostatniego wydania:

| Część | Kiedy | Przykład |
|---|---|---|
| **X** (główna) | duża zmiana, przy której ludzie muszą się czegoś nauczyć albo coś przestaje działać jak dawniej: nowy sposób pracy, usunięta funkcja, zmiana danych wymagająca ręcznych kroków | 1.4.2 → **2.0.0** |
| **Y** (funkcje) | co najmniej jedna nowa funkcja albo widoczna zmiana działania | 1.4.2 → 1.**5**.0 |
| **Z** (poprawki) | same poprawki błędów i drobne zmiany, nic nowego | 1.4.2 → 1.4.**3** |

Po podniesieniu liczby te na prawo od niej wracają do zera. Wątpliwość „funkcja czy poprawka”: jeśli warto to ogłosić użytkownikom, to funkcja.

## Wersja a wdrożenie

- **Każdy merge do `master` trafia na serwer** - tak jak dotąd. Nie trzeba robić wydania, żeby poprawka zadziałała u ludzi.
- **Wydanie** to decyzja, że zebrane zmiany dostają numer i opis (np. przed turniejem, po większej funkcji, raz na kilka tygodni).
- Aplikacja pokazuje, co dokładnie działa na serwerze:
  - `1.4.0` - dokładnie wydanie 1.4.0,
  - `1.4.0+3 (abc1234)` - wydanie 1.4.0 i 3 zmiany po nim (ostatnia to commit `abc1234`).

  Numer jest w stopce strony i portalu kibica oraz pod `https://TWOJA-DOMENA/backend/version`. Przy zgłoszeniu błędu podaj go.

## Lista zmian: CHANGELOG.md

- Każdy PR, który zmienia coś widocznego dla użytkownika, dopisuje jedną linijkę w sekcji **„Nieopublikowane”** w [../CHANGELOG.md](../CHANGELOG.md).
- Linijka z perspektywy użytkownika, po polsku: „Trzecie miejsce widać obok mistrza”, a nie „Add bracketThirdPlace”.
- Kategorie: **Nowe**, **Zmienione**, **Poprawione**, **Usunięte**. Zmiany czysto techniczne (testy, porządki, CI) nie trafiają do listy.
- Z kategorii od razu widać, którą liczbę podnieść: coś w „Nowe” albo „Zmienione” → Y, same „Poprawione” → Z.

## Okno „Co nowego”

- Po zalogowaniu, gdy na serwerze jest wydanie nowsze niż ostatnio obejrzane, aplikacja pokazuje **jedno** okno z punktami „Nowe” i „Zmienione” ze wszystkich pominiętych wydań (najwyżej 5, reszta pod linkiem). Pełna lista: strona `/changelog` (link „Co nowego” w stopce).
- Ostatnio obejrzana wersja jest zapisana na koncie, więc okno nie wraca na innym urządzeniu. Nowe konto okna nie dostaje.
- Linijki w „Nowe” i „Zmienione” pisz więc tak, żeby czytał je organizator, trener albo rodzic.
- Treść pochodzi z `Turniejo-frontend/src/features/whats-new/releases.json`, kopii wydanych wersji z CHANGELOG.md. `release.sh prepare` aktualizuje ją sam. Ręczna poprawka opisu wydanej wersji: `cd Turniejo-frontend && npm run changelog:sync` (test w `check.sh` pilnuje zgodności).
- Sekcja „Nieopublikowane” nie trafia do okna, nawet jeśli jest już na serwerze.

## Jak wydać wersję

```bash
# 1. Przygotowanie: branch release/1.5.0, „Nieopublikowane” staje się [1.5.0] z datą
bash scripts/release.sh prepare 1.5.0
git push -u origin release/1.5.0          # PR do master, przejrzyj listę zmian, merge

# 2. Po merge: tag na master
bash scripts/release.sh tag 1.5.0
```

Po wypchnięciu tagu GitHub Actions:

- tworzy **GitHub Release** `Turniejo v1.5.0` z opisem z CHANGELOG.md (zakładka Releases w repozytorium),
- wdraża ten sam kod jeszcze raz, żeby serwer pokazywał dokładnie `1.5.0`.

## Czego nie używamy

- `version` w `Turniejo-frontend/package.json` i wersja w projektach .NET nie są aktualizowane. Jedynym źródłem numeru jest tag w gicie (`vX.Y.Z`).
- Nie wersjonujemy osobno backendu i frontendu: to jedna aplikacja, wdrażana razem, z jednym numerem.
