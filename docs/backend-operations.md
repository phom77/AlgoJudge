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

API and worker console logs are JSON. Request logs and worker events use named
properties so a log platform can index fields such as request path, status,
elapsed time, worker ID, submission ID, and trace ID.

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

## Database migrations

`Database:MigrateOnStartup` defaults to `false`. Compose enables it for the
single local API instance. Production deployments should execute migrations as
a dedicated release task before starting or rolling out multiple API replicas.

## Worker isolation

The worker remains a separate host process because it needs the local Docker
Engine to launch the hardened judge sandbox. The API and PostgreSQL can run in
Compose without granting Docker socket access to the API container.
