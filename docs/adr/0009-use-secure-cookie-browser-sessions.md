# ADR-0009: Use secure cookie sessions for the browser client

Status: Accepted
Date: 2026-07-16

## Context

The original authentication contract returned access and refresh tokens in JSON
for clients to store and attach as Bearer credentials. The Angular client has
not yet been scaffolded, so there is no released browser consumer, but retaining
this contract would expose long-lived credentials to JavaScript storage and any
successful XSS payload.

Cookie authentication also introduces CSRF risk because browsers attach cookies
automatically. SameSite is defense in depth but is not the sole request
verification mechanism.

## Decision

Issue the JWT access credential and opaque refresh credential only as cookies:

- access: host-only, path `/`, `HttpOnly`, `Secure`, `SameSite=Strict`;
- refresh: no Domain attribute, path `/api/auth`, `HttpOnly`, `Secure`,
  `SameSite=Strict`;
- neither credential is returned in a public response or readable by Angular.

Use ASP.NET Core antiforgery validation for unsafe cookie-authenticated API
requests. `GET /api/auth/csrf` issues antiforgery state; Angular mirrors the
readable `XSRF-TOKEN` cookie in the `X-XSRF-TOKEN` header. Serve the SPA and API
from one origin, using an Angular development proxy locally.

Keep explicit Bearer-header authentication for non-browser machine clients.
Such requests do not rely on ambient cookies and are exempt from browser CSRF
validation.

Add `GET /api/auth/session`; make refresh and revoke bodyless because they read
the restricted HttpOnly refresh cookie. Logout revokes the stored refresh token
and deletes both credential cookies.

This is an intentional final pre-frontend reset of the OpenAPI v1 authentication
contract. No browser client has been released, and the new snapshot becomes the
baseline used to generate the first Angular client. Further breaking changes
require a new API document version.

## Consequences

- XSS cannot directly read access or refresh credential values.
- Unsafe browser requests require antiforgery state in addition to SameSite.
- Angular never stores or attaches authentication credentials itself.
- Local development and production need a same-origin SPA/API topology.
- Cross-origin browser deployments require a new reviewed cookie and CORS
  decision rather than ad hoc configuration.
- OpenAPI and integration tests must prove credential fields are absent and
  cookie flags, refresh rotation, revocation, and CSRF rejection remain stable.
