#!/usr/bin/env bash
# Dump obu baz (aplikacja + Keycloak) do ~/backups. Pliki starsze niż KEEP_DAYS są usuwane.
# Uruchamiany przez deploy.sh przed każdym wdrożeniem i codziennie z crona (patrz README).
# Odtworzenie: pg_restore -U postgres -d <baza> --clean <plik>.dump (opis w README).
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_DIR="${BACKUP_DIR:-$HOME/backups}"
KEEP_DAYS="${KEEP_DAYS:-14}"
COMPOSE=(docker compose --project-directory "$DEPLOY_DIR" -f "$DEPLOY_DIR/docker-compose.prod.yml")

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

# Pierwsze wdrożenie: bazy jeszcze nie ma, więc nie ma czego zrzucać.
if [ -z "$("${COMPOSE[@]}" ps --status running -q postgres 2>/dev/null)" ]; then
  echo "backup: postgres nie działa, pomijam"
  exit 0
fi

stamp="$(date +%F_%H%M)"
for db in turniejo keycloak; do
  file="$BACKUP_DIR/$db-$stamp.dump"
  "${COMPOSE[@]}" exec -T postgres pg_dump -U postgres -Fc "$db" > "$file.tmp"
  mv "$file.tmp" "$file"
  echo "backup: $file ($(du -h "$file" | cut -f1))"
done

find "$BACKUP_DIR" -name '*.dump' -mtime +"$KEEP_DAYS" -delete
