# ADR-0007: Use PostgreSQL for the MVP submission queue

Status: Accepted
Date: 2026-07-15

## Context

The grading worker must claim Pending submissions without allowing two workers
to execute the same attempt concurrently. A worker can crash after claiming,
so ownership must expire and be recoverable. AlgoJudge already requires
PostgreSQL, while the expected MVP queue volume does not yet justify another
operational dependency.

## Decision

Use PostgreSQL as the durable MVP submission queue. Claim one row with a single
`FOR UPDATE SKIP LOCKED` statement that changes it from Pending to Running and
assigns a worker ID, unique claim token, lease expiry, and attempt number.

The claim token acts as a fencing token. Lease renewal, final verdict writes,
and retry release are conditional on submission ID, worker ID, claim token, and
Running status. An expired claim can be reclaimed. Claims that exhaust the
configured attempt limit are finalized as Runtime Error.

The worker renews its lease on a separate dependency-injection scope so the
heartbeat never uses the grader's `DbContext` concurrently.

## Consequences

- Multiple workers can poll the same PostgreSQL database safely.
- A crashed worker does not leave a submission permanently Running.
- Stale workers cannot overwrite a result after losing ownership.
- Queue latency and database load must be measured before increasing worker
  count or polling frequency substantially.
- Redis and RabbitMQ are not required for the MVP queue.

## Alternatives considered

- RabbitMQ: deferred until measured throughput or delivery requirements exceed
  the PostgreSQL queue.
- Redis: rejected as the durable queue because submissions already require
  transactional PostgreSQL state.
- Reading batches of Pending rows without locking: rejected because concurrent
  workers can grade the same submission.
