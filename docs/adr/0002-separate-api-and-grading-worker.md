# ADR-0002: Run grading as a separately deployable worker

Status: Proposed
Date: 2026-07-15

## Context

The current implementation runs a polling `BackgroundService` inside the web
API process. Code execution is slow, resource-intensive, and has different
failure and scaling behaviour from request handling.

## Decision

The production architecture will run the API and grading worker as separate
processes/services. They share a durable submission queue/claim mechanism in
the data layer. Claiming, lease expiry, and final state updates are atomic and
idempotent.

## Consequences

- Web API capacity can scale independently from judge capacity.
- A crashed worker can be recovered without taking down the API.
- Worker deployment needs access to a secure sandbox runtime, while the API
  should not need that privilege.
- Local development must run both services.

## Alternatives considered

- Keep the hosted worker inside the API: rejected for production because it
  couples request serving and untrusted execution, and makes safe scaling
  difficult.
- Introduce an external message broker immediately: deferred. PostgreSQL-based
  atomic claiming may be sufficient for MVP; the concrete choice requires a
  later ADR after load testing.
