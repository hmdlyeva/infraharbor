# Developer tooling

Task: IH-002

InfraHarbor keeps local prerequisites intentionally small and standard.

## Required

| Tool | Project baseline | Purpose |
| --- | --- | --- |
| Git | 2.40+ | source control |
| Node.js | 24 LTS | Next.js runtime/tooling |
| pnpm | 11.x | JavaScript workspace/package manager |
| .NET SDK | 10.0.x | API and Worker development |
| Docker | current supported stable | local PostgreSQL and production packaging |
| Docker Compose | Compose v2 | multi-service local/prod stack |

The project commits `.nvmrc`, `packageManager`, and `global.json` hints so contributors have a reproducible baseline without hard-coding one operating-system installer.

## Recommended installation path

### macOS

- Install Git with Xcode Command Line Tools or Homebrew.
- Install Node 24 LTS with a version manager such as `nvm`, `fnm`, or the official installer.
- Enable pnpm through Corepack or install the documented pnpm major version.
- Install the .NET 10 SDK from Microsoft.
- Install Docker Desktop or a compatible Docker Engine + Compose v2 setup.

### Windows

- Install Git for Windows.
- Install Node 24 LTS.
- Enable/install pnpm.
- Install the .NET 10 SDK.
- Install Docker Desktop with the WSL2 backend where appropriate.

### Linux

Use the distribution/vendor-supported installation methods for Git and Docker, and the official Node/.NET repositories or version managers.

## Verify

Run:

```bash
./scripts/check-tooling.sh
```

The script verifies that the commands exist and prints detected versions. It intentionally does not auto-install system software or elevate privileges.

## CI baseline

GitHub Actions uses clean hosted runners and installs the required Node/.NET versions explicitly. Local contributors do not need to match a specific patch release when they are on a supported project major/LTS line, unless a lockfile/build issue requires a temporary pin.
