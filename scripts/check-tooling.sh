#!/usr/bin/env bash
set -euo pipefail

missing=0

check() {
  local name="$1"
  shift
  if command -v "$name" >/dev/null 2>&1; then
    printf '%-12s %s\n' "$name" "$($@ 2>/dev/null | head -n 1)"
  else
    printf '%-12s %s\n' "$name" 'MISSING'
    missing=1
  fi
}

check git git --version
check node node --version
check pnpm pnpm --version
check dotnet dotnet --version
check docker docker --version

if command -v docker >/dev/null 2>&1; then
  if docker compose version >/dev/null 2>&1; then
    printf '%-12s %s\n' 'compose' "$(docker compose version | head -n 1)"
  else
    printf '%-12s %s\n' 'compose' 'MISSING'
    missing=1
  fi
fi

exit "$missing"
