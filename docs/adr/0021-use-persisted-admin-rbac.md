# ADR-0021: Use persisted Admin RBAC for administration

Status: Accepted
Date: 2026-07-27
Supersedes: [ADR-0015](0015-use-configured-internal-maintainer-access.md)

## Context

The maintainer allowlist in ADR-0015 protected the first internal authoring
workflow, but it did not model administration in the product. The browser also
could not determine whether the authenticated user was a maintainer before it
showed the authoring navigation. AlgoJudge now needs a clear, durable boundary
between regular learners and administrators.

## Decision

Persist one required `UserRole` value on every `User`: `User` or `Admin`.
Existing users migrate to `User`, and public registration always creates a
`User`; the registration request never accepts a role or privilege field.

The API issues the role as a signed JWT role claim and exposes only the
non-sensitive `isAdmin` capability in the existing session response. Browser
route guards and navigation use that capability for UX only; every internal
administration route requires the server-side `Admin` policy.

The first and recovery administrators are promoted one way at API startup from
the operator-owned `AdminBootstrap:Emails` configuration. The application only
promotes existing accounts matching those configured email addresses. Removing
an address does not demote an existing administrator. Operators must use a
reviewed database migration or a future audited administration workflow for
demotion or additional role changes.

## Consequences

- The former `MaintainerAccess:UserIds` allowlist and `Maintainer` policy are
  removed.
- A role change takes effect after the user receives a newly issued access
  session through login or refresh.
- The public authentication contract adds `isAdmin` without exposing tokens,
  passwords, or an administrative role-management endpoint.
- Later admin problem-management APIs may rely on the `Admin` policy, but must
  still enforce resource ownership where the operation requires it.
- The bootstrap email list is deployment configuration, not a secret. It must
  remain outside tracked production configuration and be changed through normal
  operational review.
