#!/usr/bin/env bash
# Kontrola przed oddaniem zmiany - to samo co CI, w jednej komendzie (Git Bash na Windows też działa).
#
#   bash scripts/check.sh          backend (wszystkie testy) + frontend: lint, typy, Vitest
#   bash scripts/check.sh --e2e    + E2E na mocku (Playwright, telefon i desktop)
#   bash scripts/check.sh --front  tylko frontend (bez backendu i E2E)
#
# Wymaga Dockera (testy integracyjne backendu stawiają Postgresa w Testcontainers).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUN_BACKEND=1
RUN_E2E=0
for arg in "$@"; do
  case "$arg" in
    --e2e) RUN_E2E=1 ;;
    --front) RUN_BACKEND=0 ;;
    *) echo "Nieznana opcja: $arg" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[1;34m== %s\033[0m\n' "$1"; }

if [ "$RUN_BACKEND" = 1 ]; then
  # Osobny katalog wyjściowy: uruchomione lokalnie API nie blokuje plików w bin/.
  ARTIFACTS="${TMPDIR:-${TEMP:-/tmp}}/turniejo-check"
  step "Backend: testy"
  dotnet test "$ROOT/Turniejo-backend/Turniejo.slnx" --nologo --artifacts-path "$ARTIFACTS"
  step "Backend: testy architektury"
  dotnet test "$ROOT/Turniejo-backend/tests/Turniejo.Architecture.UnitTests" --nologo --artifacts-path "$ARTIFACTS"
fi

cd "$ROOT/Turniejo-frontend"
step "Frontend: lint"
npm run lint
step "Frontend: typy"
API_URL=http://localhost:8080 npx next typegen > /dev/null
npm run typecheck
step "Frontend: testy jednostkowe"
npm test

if [ "$RUN_E2E" = 1 ]; then
  step "Frontend: E2E na mocku"
  npx playwright test
fi

printf '\n\033[1;32mWszystko zielone.\033[0m\n'
