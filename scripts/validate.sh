#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
./scripts/check-tooling.sh
pnpm lint:web
pnpm typecheck:web
pnpm build:web
dotnet format InfraHarbor.slnx --verify-no-changes --no-restore
dotnet build InfraHarbor.slnx --configuration Release
dotnet test InfraHarbor.slnx --configuration Release --no-build
