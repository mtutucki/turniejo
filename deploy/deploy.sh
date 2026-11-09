#!/usr/bin/env bash
# Wdrożenie najnowszej wersji z gałęzi DEPLOY_BRANCH (domyślnie master):
#   backup baz → git pull → build i restart kontenerów → sprawdzenie /backend/health.
# Uruchamiany ręcznie na serwerze albo przez GitHub Actions (.github/workflows/deploy.yml).
#
# Cała logika siedzi w funkcji main wywołanej na końcu: bash wczytuje ją w całości przed startem,
# więc git pull, który podmienia ten plik, nie psuje trwającego wdrożenia.
set -euo pipefail

main() {
  local deploy_dir repo_dir branch domain
  deploy_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  repo_dir="$(dirname "$deploy_dir")"
  branch="${DEPLOY_BRANCH:-master}"
  local compose=(docker compose --project-directory "$deploy_dir" -f "$deploy_dir/docker-compose.prod.yml")

  # Jedno wdrożenie naraz (np. dwa szybkie pushe).
  exec 9>"$deploy_dir/.deploy.lock"
  flock -n 9 || { echo "Inne wdrożenie już trwa"; exit 1; }

  if [ ! -f "$deploy_dir/.env" ]; then
    echo "Brak $deploy_dir/.env (skopiuj .env.example i uzupełnij)"
    exit 1
  fi
  domain="$(grep -E '^DOMAIN=' "$deploy_dir/.env" | cut -d= -f2-)"

  echo "==> Backup baz"
  "$deploy_dir/backup.sh"

  echo "==> Pobieranie kodu ($branch)"
  git -C "$repo_dir" fetch --prune --tags origin "$branch"
  # reset zamiast pull: serwer zawsze ma dokładnie to, co na GitHubie (lokalne zmiany w plikach
  # z repo są nadpisywane, .env nie jest w gicie, więc zostaje).
  git -C "$repo_dir" reset --hard "origin/$branch"
  # Wersja z ostatniego tagu wydania, np. v1.2.0 albo v1.2.0-3-gabc1234 (3 commity po wydaniu).
  # Trafia do stopki aplikacji i pod /backend/version (docs/wersjonowanie.md).
  APP_VERSION="$(git -C "$repo_dir" describe --tags --always)"
  export APP_VERSION
  echo "Wersja: $APP_VERSION ($(git -C "$repo_dir" log -1 --format='%s'))"

  echo "==> Build i restart kontenerów"
  "${compose[@]}" up -d --build --remove-orphans

  echo "==> Sprawdzanie, czy aplikacja działa"
  # --resolve: zapytanie do Caddy na tym serwerze, bez wychodzenia przez publiczny IP.
  local url="https://$domain/backend/health"
  for _ in $(seq 1 36); do
    if curl -fsS --max-time 5 --resolve "$domain:443:127.0.0.1" "$url" >/dev/null 2>&1; then
      echo "OK: $url odpowiada"
      docker image prune -f >/dev/null
      echo "==> Gotowe"
      return 0
    fi
    sleep 5
  done

  echo "BŁĄD: $url nie odpowiada po 3 minutach. Ostatnie logi API:"
  "${compose[@]}" logs --tail 50 api
  exit 1
}

main "$@"
