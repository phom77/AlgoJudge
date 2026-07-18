# Backend operations baseline

## Configuration

The API fails during startup when its PostgreSQL connection string, JWT issuer,
JWT audience, JWT secret, token lifetimes, or rate-limit values are invalid. The
JWT secret must contain at least 32 characters. Secrets belong in environment
variables or a secret manager and are intentionally empty in `appsettings.json`.

The worker validates its PostgreSQL connection, queue lease settings, and pinned
sandbox image configuration before starting.

## Health endpoints

Both processes expose two unauthenticated operational endpoints:

- `/health/live` reports whether the process and its background service are
  running.
- `/health/ready` additionally verifies PostgreSQL connectivity. A load balancer
  should send traffic only to ready instances.

The API endpoints are excluded from rate limiting. PostgreSQL also has a native
Compose health check based on `pg_isready`.

## Errors and logging

API errors use Problem Details responses with a stable status, title, type,
detail, instance, `code`, and `traceId`. Validation failures additionally expose
an `errors` dictionary. Expected application failures map to HTTP 400, 401, 403,
404, or 409. Unexpected exceptions are logged with their stack traces and
return a non-sensitive HTTP 500 response.

API and worker console logs are compact, colored text in Development so local
terminal output remains readable. Non-Development environments emit structured
JSON. Request logs and worker events use named properties so a log platform can
index fields such as request path, status, elapsed time, worker ID, submission
ID, and trace ID. Request completion uses Information for successful responses,
Warning for 4xx responses, and Error for 5xx responses.

Judge logs record container status, exit codes, output byte counts, and
truncation flags when external commands fail. Raw compiler, runner, stdout,
stderr, source code, and hidden testcase payloads are not written to normal
logs. Expected API failures are logged by status, error type, and exception
type without copying their detail text into the log event.

## Rate limiting

The API uses a fixed-window global limiter partitioned by authenticated user ID
or client IP. Rejected requests return HTTP 429 Problem Details and a
`Retry-After` header when available. This in-memory limiter is appropriate for a
single API instance. Multiple API replicas require a gateway or distributed
rate-limit store before their limits can be globally consistent.

## OpenAPI

The versioned contract is always available at `/openapi/v1.json`. Its document
name and API version remain stable for the lifetime of v1. Breaking route or
schema changes require a new API document version.

OpenAPI declares the secure cookie session scheme. Submission endpoints and
refresh-token revocation require it; problem catalogue endpoints declare it as
optional so a browser session can receive solved state. CI compares the generated
document with the approved semantic snapshot. After reviewing an intentional
contract change, regenerate it with:

```powershell
./scripts/update-openapi-snapshot.ps1
```

Browser credentials are issued only through HttpOnly, Secure, SameSite=Strict
cookies. The SPA and API must be served from one origin. Angular development
uses a same-origin proxy rather than broad credentialed CORS. Unsafe
cookie-authenticated API requests require the `X-XSRF-TOKEN` header paired with
the antiforgery cookies issued by `GET /api/auth/csrf`.

The HTTP-only local Development profile uses unprefixed, non-Secure cookie names
so the Angular development proxy can bootstrap antiforgery without TLS. Testing
and every non-Development environment retain the `__Host`/`__Secure` names and
`Secure` cookie policy. Never enable the Development cookie policy in a deployed
environment.

ASP.NET Data Protection protects antiforgery and authentication state. Local
Development persists its keys under the ignored repository `.local` directory
to avoid relying on a potentially inaccessible user-profile key ring. Production
must set `DataProtection:KeysPath` to storage that persists across restarts and
is shared by every API replica; the key directory is sensitive operational data
and must never be committed.

## Database migrations

`Database:MigrateOnStartup` defaults to `false`. Compose enables it for the
single local API instance. Production deployments should execute migrations as
a dedicated release task before starting or rolling out multiple API replicas.

## Worker isolation

The worker remains a separate host process because it needs the local Docker
Engine to launch the hardened judge sandbox. The API and PostgreSQL can run in
Compose without granting Docker socket access to the API container.

## Backend acceptance

Run `./scripts/test-backend-e2e.ps1` before treating the backend workflow as
release-ready. The suite connects the HTTP API, a migrated PostgreSQL queue,
one or two real grading workers, and the pinned C++17 Docker image. It verifies
all final verdicts, solved state, ownership and private-data boundaries,
expired-lease recovery, stale-worker fencing, and duplicate-claim prevention.

The ordinary solution test command may skip this suite when PostgreSQL or the
judge image is unavailable. The dedicated `backend-e2e-acceptance` CI job must
provide both prerequisites and run the suite without skips.
