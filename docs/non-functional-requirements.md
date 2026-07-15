# Non-Functional Requirements

## 1. Security

- Passwords use an adaptive one-way hash; plaintext passwords are never logged.
- JWT signing keys, connection strings, and image registry credentials are
  supplied through secrets/environment configuration, never committed to Git.
- Public registration creates only regular users; there is no user-controlled
  role claim.
- Every authenticated resource enforces ownership on the server.
- Hidden testcase input/output is absent from public DTOs, normal logs, error
  messages, and client telemetry.
- Submission endpoints enforce per-user and per-IP rate limits.
- Uploaded content archives are checked against zip-slip, zip bombs, duplicate
  names, and compressed/uncompressed size limits.
- The judge follows the isolation requirements in `judge-spec.md`.

## 2. Reliability and correctness

- Database changes are applied by versioned migrations.
- A submission reaches one final verdict or is recoverable after a worker crash.
- A final submission result is never overwritten by another worker.
- Problem publication validates all required content before visibility changes.
- Backups and restore steps are documented and periodically tested.

## 3. Initial performance targets

These are MVP targets to validate in staging, not promises before capacity
testing.

| Area | Initial target |
|---|---|
| Public list/detail API | p95 under 300 ms excluding network latency |
| Submission creation | p95 under 500 ms; execution occurs asynchronously |
| Queue pickup | typical Pending-to-Running under 10 seconds at normal load |
| Catalogue pagination | bounded page size, default 20 and maximum 100 |
| Judge work | bounded by per-problem resource limits and worker concurrency |

## 4. Observability

- Use structured logs with request ID, submission ID, worker ID, and problem ID
  when appropriate.
- Never log source code, credentials, access tokens, or hidden testcase data at
  normal log levels.
- Expose health checks separately for API, database connectivity, and worker
  readiness.
- Record metrics for queue depth, claim failures, verdict distribution, judge
  duration, sandbox errors, rate-limit events, and failed authentication.
- Alert on a sustained queue backlog, high sandbox fault rate, or repeated
  worker recovery attempts.

## 5. Maintainability

- Preserve a clear dependency direction: web/API depends on application;
  infrastructure implements application ports; domain has no framework
  dependency.
- Keep judge abstractions independent of Docker so a stronger runtime can be
  introduced later.
- New behaviour requires automated tests at the appropriate level.
- Public API changes are documented and versioned deliberately.
- Formatting, static analysis, build, test, and vulnerability scanning run in
  CI on every pull request.

## 6. Deployment and data

- Local development must be reproducible with documented prerequisites and a
  Compose-based PostgreSQL environment.
- API, worker, and database are separately configurable and deployable.
- Production runs migrations through a controlled deployment step, not an
  unreviewed application startup side effect.
- UTC is used for persisted timestamps and API timestamps use ISO 8601.
- Personal data is limited to account data needed for the product. Account and
  submission deletion/retention policy must be defined before public launch.
