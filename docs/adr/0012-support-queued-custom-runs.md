# ADR-0012: Support queued custom-input runs

Status: Accepted
Date: 2026-07-18

## Context

Learners need to execute a solution with their own input before submitting it
against the hidden suite. A custom run must use the same hostile-code sandbox
without creating an Accepted verdict, changing solved state, or revealing any
hidden testcase data. It must also preserve the existing PostgreSQL submission
queue's lease and stale-worker fencing guarantees.

## Decision

Add an authenticated `CodeRun` resource and a separate durable PostgreSQL
queue. `POST /api/problems/{slug}/runs` creates a Pending run and
`GET /api/runs/{id}` returns only an owner-visible status, bounded stdout and
stderr, and resource measurements. Source and input are never returned.

StdinStdout problems accept raw `input`. Function problems accept an
`arguments` JSON object validated against the stored signature and normalized
for the private adapter. A run compiles once and executes exactly once; it does
not load or compare hidden testcases. Successful execution is `Completed`, not
`Accepted`.

The worker alternates priority between submission and run queues and processes
only one claimed item at a time. Both queues retain independent renewable
leases, claim tokens, bounded retries, atomic `FOR UPDATE SKIP LOCKED` claims,
and conditional finalization.

## Consequences

- Runs never appear in submission history and never contribute to solved state.
- Custom input and output are private to the owning user and remain bounded by
  the existing sandbox controls.
- One worker cannot run a submission and custom run concurrently; alternating
  priority prevents either queue from starving the other.
- Run retention and cleanup policy may be added later without changing the
  public v1 resource shape.

## Alternatives considered

- Reuse `Submission`: rejected because a run has no hidden-suite verdict and
  must not affect history or solved state.
- Execute synchronously in the API: rejected because hostile compilation and
  execution belong in the worker and can outlive an HTTP request.
- Add a second concurrent worker loop: rejected because it could double the
  sandbox capacity assumed by current deployment limits.
