# ADR-0005: Use Angular for the web client

Status: Accepted
Date: 2026-07-15

## Context

AlgoJudge needs a browser application for problem discovery, code editing,
submission tracking, and account flows. Backend correctness and API stability
are the current priority, so frontend implementation will begin later.

## Decision

Use Angular for the web client and keep it in the repository's `web` boundary.
Do not scaffold the Angular workspace until the backend problem, authentication,
submission, and judge contracts are stable.

## Consequences

- Frontend architecture and tooling decisions can target Angular explicitly.
- The OpenAPI contract can later generate a typed Angular API client.
- Backend development does not depend on an incomplete frontend workspace.

## Alternatives considered

- Select a framework later: rejected because Angular has already been chosen.
- Scaffold Angular immediately: deferred to avoid coupling UI work to unstable
  backend contracts.
