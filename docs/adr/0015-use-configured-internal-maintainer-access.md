# ADR-0015: Use configured internal maintainer access for authoring

Status: Accepted
Date: 2026-07-22

## Context

Source authoring needs authenticated internal endpoints before the product has a
public administration or role-management model. The MVP excludes a Teacher role,
and public registration must never accept a privileged role claim. Generation
source, diagnostics, and candidate testcases are private content.

## Decision

Authorize the internal authoring API with an operator-managed allowlist of user
UUIDs supplied through `MaintainerAccess:UserIds`. Registration and account data
remain unchanged. The allowlist is deployment configuration, not a client claim
or a mutable database role.

Every authoring revision also stores its owner. A configured maintainer may only
read or mutate revisions they own. Internal authoring routes are excluded from
the stable public OpenAPI v1 document and return suite statistics rather than
candidate input or expected output.

This is a transitional internal security boundary. A future administration
product may supersede it with centrally managed RBAC through a new ADR and API
version, but must preserve registration safety and revision ownership.

## Consequences

- An empty allowlist denies every user.
- No Teacher/Admin role is introduced into the public account domain.
- Revoking access is an operational configuration change and does not transfer
  revision ownership.
- Internal clients require a separately maintained contract; public OpenAPI v1
  remains unchanged.
