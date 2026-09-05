# ADR-0001: Stack and monorepo architecture

- Status: Accepted
- Date: 2026-09-07
- Decision owners: InfraHarbor maintainers
- Task: IH-001

## Context

InfraHarbor is an open-source, self-hostable, white-label infrastructure operations dashboard. It needs a browser UI, a persistent API, background monitoring, secure remote Linux connectivity, PostgreSQL persistence, Docker-based packaging, and a contributor-friendly repository.

The product must remain useful without a proprietary hosted control plane and must not couple its core domain to one source-control or cloud provider.

## Decision

InfraHarbor uses one public monorepo with the following primary components:

- `apps/web`: Next.js + TypeScript + Tailwind CSS + shadcn/ui.
- `src/InfraHarbor.Api`: ASP.NET Core .NET 10 REST API.
- `src/InfraHarbor.Application`: application/use-case layer.
- `src/InfraHarbor.Domain`: domain entities, value objects and policies.
- `src/InfraHarbor.Infrastructure`: persistence, encryption, SSH and external adapters.
- `src/InfraHarbor.Worker`: background monitoring and integration processing.
- `tests`: backend, frontend and end-to-end automated tests.
- `deploy`: Docker, Docker Compose and reverse-proxy reference assets.
- `docs`: architecture, development, operations and roadmap documentation.
- `.github`: CI, issue templates and repository automation.

PostgreSQL is the system of record. EF Core with Npgsql is the persistence path. Agentless SSH is the first server-connection method, but arbitrary browser-triggered shell execution is not part of the product. Remote operations are implemented as allow-listed handlers.

GitHub is the first source-control/deployment integration, behind provider-neutral application interfaces.

## Dependency direction

The intended backend dependency direction is:

`Domain <- Application <- Infrastructure <- Api/Worker`

`Domain` has no dependency on ASP.NET Core, EF Core, SSH, GitHub SDKs or infrastructure adapters. `Application` defines use cases and integration abstractions. `Infrastructure` implements external concerns. `Api` and `Worker` are composition/runtime entry points.

## Deployment boundary

The canonical production distribution is persistent containers using Docker Compose. The Next.js UI may be deployed to Vercel for preview environments, but the API and worker are not treated as Vercel-native workloads because they need persistent runtime behavior and infrastructure network access.

## White-label boundary

Visible product identity is runtime configuration. Downstream forks must not need to edit business logic to replace product name, logo, favicon, support/documentation URLs, footer copy or supported theme tokens.

## Security boundary

InfraHarbor must not expose an unrestricted web shell. Stored remote credentials are encrypted. SSH host-key changes fail closed. Infrastructure mutations require explicit authorization and produce audit records.

## Consequences

### Positive

- One repository versions UI, API, worker, deployment assets and docs together.
- The architecture supports self-hosting and downstream forks.
- Provider integrations can evolve without leaking vendor payload types into the domain.
- Security-sensitive infrastructure operations remain centralized and testable.

### Trade-offs

- A monorepo requires coordinated frontend/backend CI.
- A persistent API/worker means a pure serverless production deployment is not supported.
- Agentless SSH is operationally convenient but increases the importance of credential encryption, host-key verification and strict command allow-listing.

## Review triggers

Revisit this ADR if the project introduces Kubernetes orchestration, an agent-based connectivity model, true multi-tenancy, or a hosted control plane that materially changes these boundaries.
