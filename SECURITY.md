# Security Policy

InfraHarbor is infrastructure-management software. Please do not disclose suspected vulnerabilities in a public issue before maintainers have had a reasonable opportunity to assess them.

## Reporting

The project owner must configure a private security-reporting channel before the first public beta. Until that channel is published, do not post proof-of-concept secrets, private keys, real infrastructure addresses or exploit details in public project content.

A useful report includes affected version/commit, impact, reproduction conditions and a minimal safe proof of concept.

## Scope priorities

High-priority areas include:

- authentication and authorization bypass;
- plaintext or recoverable secret exposure;
- SSH host-key trust bypass;
- command injection or unrestricted remote command execution;
- webhook signature bypass;
- SSRF that reaches sensitive metadata/control endpoints;
- audit-log tampering;
- cross-project resource access.

## Supported versions

Before v1.0, only the latest development/release line is expected to receive fixes. A formal supported-version matrix will be published with stable releases.
