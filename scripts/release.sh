#!/usr/bin/env bash
# Wydanie nowej wersji w dwóch krokach (zasady: docs/wersjonowanie.md).
#
#   bash scripts/release.sh prepare 1.2.0   branch release/1.2.0: sekcja „Nieopublikowane” w CHANGELOG.md
#                                           staje się [1.2.0] z dzisiejszą datą; potem PR i merge
#   bash scripts/release.sh tag 1.2.0       po merge: tag v1.2.0 na aktualnym master i push tagu
#                                           (GitHub Actions: Release z notatkami + wdrożenie z numerem wersji)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CHANGELOG="$ROOT/CHANGELOG.md"
UNRELEASED="## [Nieopublikowane]"

die() { echo "BŁĄD: $*" >&2; exit 1; }

command="${1:-}"
version="${2:-}"
[[ "$command" =~ ^(prepare|tag)$ ]] || die "użycie: bash scripts/release.sh prepare|tag X.Y.Z"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "wersja w formacie X.Y.Z, np. 1.2.0"

cd "$ROOT"
[ -z "$(git status --porcelain)" ] || die "masz niezatwierdzone zmiany - najpierw commit albo stash"
git fetch --quiet --tags origin master
git rev-parse -q --verify "refs/tags/v$version" >/dev/null && die "tag v$version już istnieje"

case "$command" in
  prepare)
    # Treść „Nieopublikowane”: od nagłówka do następnej sekcji wersji.
    notes="$(awk -v head="$UNRELEASED" '
      index($0, head) == 1 { found = 1; next }
      found && /^## \[/ { exit }
      found { print }
    ' "$CHANGELOG")"
    [ -n "$(echo "$notes" | tr -d '[:space:]')" ] || die "sekcja „Nieopublikowane” w CHANGELOG.md jest pusta - nie ma czego wydać"

    git switch --quiet -c "release/$version" origin/master
    today="$(date +%F)"
    awk -v head="$UNRELEASED" -v release="## [$version] - $today" '
      index($0, head) == 1 { print; print ""; print release; next }
      { print }
    ' "$CHANGELOG" > "$CHANGELOG.tmp" && mv "$CHANGELOG.tmp" "$CHANGELOG"
    git add "$CHANGELOG"
    git commit --quiet -m "Release $version"
    echo "Gotowe: branch release/$version z wpisem [$version] - $today."
    echo "Dalej: git push -u origin release/$version, PR do master, merge, potem:"
    echo "  bash scripts/release.sh tag $version"
    ;;

  tag)
    git switch --quiet master
    git merge --quiet --ff-only origin/master || die "lokalny master rozjechał się z origin/master"
    grep -q "^## \[$version\]" "$CHANGELOG" || die "CHANGELOG.md na master nie ma sekcji [$version] - najpierw prepare i merge"
    git tag -a "v$version" -m "Turniejo $version"
    git push --quiet origin "v$version"
    echo "Wypchnięto tag v$version. GitHub Actions utworzy Release i wdroży wersję $version."
    ;;
esac
