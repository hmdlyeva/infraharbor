# Contributing to InfraHarbor

Thank you for considering a contribution.

## Before you start

1. Read the architecture ADRs and relevant roadmap/task scope.
2. For substantial behavior or architecture changes, open an issue first.
3. Never put real credentials, hostnames/IPs, private keys, tokens or private customer data in source, fixtures, screenshots or issue content.

## Branch and task convention

Maintainer task branches use:

```text
task/IH-###-short-slug
```

Commits should be focused and reference the task when one exists:

```text
<type>(<scope>): <summary> [IH-###]
```

## Quality expectations

A behavior change should include the smallest meaningful automated test coverage. Security-sensitive changes require negative tests for authorization, unsafe input, secret handling or failure paths as applicable.

Run the repository validation commands documented in the development guide before submitting a pull request.

## Pull requests

A pull request should explain:

- what changed;
- why it changed;
- task/issue reference;
- how it was validated;
- configuration or migration impact;
- security implications when relevant.

Keep unrelated refactors out of feature pull requests.
