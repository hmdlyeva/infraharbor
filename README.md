# InfraHarbor

InfraHarbor is an open-source, self-hostable and white-label infrastructure operations dashboard for small engineering teams, agencies and product owners.

It is designed to bring Linux host visibility, controlled Docker operations, service health monitoring, incident state, delivery history and operational audit evidence into one product that downstream adopters can rebrand without forking core business logic.

> Status: early foundation work. The repository is not production-ready yet.

## Planned capabilities

- Projects and environments
- Self-hosted authentication and role-based authorization
- Linux server registry with encrypted SSH credentials and explicit host-key trust
- CPU, memory, disk and uptime visibility
- Docker inventory, bounded logs, and controlled start/stop/restart actions
- HTTP/TCP health monitoring and incident lifecycle
- Provider-neutral deployment history with GitHub as the first adapter
- Generic webhook notifications
- Append-oriented audit log
- Runtime white-label branding
- Docker Compose production packaging

## Architecture

InfraHarbor is a monorepo. See [`docs/architecture/ADR-0001-stack-and-monorepo.md`](docs/architecture/ADR-0001-stack-and-monorepo.md).

Planned runtime components:

```text
Browser
  |
  v
Next.js Web
  |
  v
ASP.NET Core API ---- PostgreSQL
  |
  +---- SSH adapters ---- Linux / Docker hosts
  |
  +---- Integration adapters

.NET Worker
  +---- monitoring scheduler
  +---- notification delivery
  +---- background integration processing
```

## Development prerequisites

See [`docs/development/tooling.md`](docs/development/tooling.md). The baseline is Node.js 24 LTS, pnpm 11, .NET 10 SDK, Git, Docker and Docker Compose v2.

Run the prerequisite check:

```bash
./scripts/check-tooling.sh
```

## Security model

InfraHarbor will operate against real infrastructure, so security boundaries are part of the product architecture:

- no unrestricted browser-triggered shell;
- remote credentials encrypted before persistence;
- SSH host-key changes fail closed;
- infrastructure mutations require backend authorization;
- privileged operations produce audit evidence;
- secrets must never be committed.

Please read [`SECURITY.md`](SECURITY.md) before reporting a vulnerability.

## White-label model

InfraHarbor is the upstream identity. Visible deployment branding is intended to come from runtime configuration rather than hard-coded UI forks. Downstream users will be able to configure the product name, logo/favicon, supported theme tokens, support/docs links and footer copy.

## Roadmap

Implementation tasks use stable IDs such as `IH-001`. Contributor-facing roadmap material lives under [`docs/roadmap`](docs/roadmap), while the detailed project tracker and feature specifications are maintained in the project workspace.

## Contributing

Contributions are welcome once the relevant area is sufficiently stable. Start with [`CONTRIBUTING.md`](CONTRIBUTING.md) and open an issue before large architectural changes.

## License

Licensed under the Apache License, Version 2.0. See [`LICENSE`](LICENSE).
