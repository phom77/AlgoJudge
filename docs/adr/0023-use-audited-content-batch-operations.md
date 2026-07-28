# ADR-0023: Use audited content batch operations

Status: Accepted
Date: 2026-07-28

## Context

ADR-0022 produces deterministic catalog definitions but deliberately stops
before persistence and generation. Importing tens or hundreds of definitions
one at a time cannot provide durable progress, independent failures, bounded
worker concurrency, safe retry, or an auditable approval boundary.

The API must not compile or execute maintainer source. Published revisions and
their immutable system suites must also remain unchanged until an Admin
explicitly approves publication.

## Decision

Persist a `ContentBatch`, ordered `ContentBatchItem` records, and append-only
`ContentBatchAuditEntry` records in PostgreSQL. A batch follows:

```text
Created -> Validating -> Generating -> ReadyForReview -> Publishing -> Completed
```

Each item independently follows:

```text
Pending -> Generating -> Ready -> Published
Pending/Generating -> Failed -> Retrying
Pending -> Skipped
```

The internal Admin API creates, lists, reads, starts, resumes, retries, and
publishes batches. Creating a batch persists materialized definitions from the
trusted ContentTool. Starting or resuming applies each catalog action with a
checkpoint after each item:

- `create` requires an absent slug;
- `update-draft` updates only an existing Draft revision;
- `new-revision` creates a successor to Published content;
- `skip` performs no import.

An item whose effective content hash matches the latest revision is marked
`Skipped`. Retry reuses the existing Draft revision and creates a new generation
job, so it cannot create a duplicate revision. A Published revision is never
edited.

Generation jobs remain in the PostgreSQL content queue. The API only snapshots
and enqueues; `AlgoJudge.ContentWorker` is the sole compiler/executor. The worker
uses bounded concurrency, renewable leases, claim tokens, and conditional
finalization. Batch jobs are claimable only after their batch reaches
`Generating`, which prevents a worker from racing the API's import checkpoints.
One failed item does not stop other items.

Publishing requires a non-empty explicit list of approved revision IDs. Every
selected item must be `Ready`; unselected Ready items remain unchanged.
Publication reuses the existing transactional revision publication workflow.

Audit records contain the Admin ID, batch/item and optional problem/revision
IDs, action, time, result, and safe failure category. Audit records, responses,
and normal logs never contain generator, validator, reference or wrong-solution
source, hidden input/output, unsanitized compiler output, or access tokens.
Private definition snapshots remain persistence-only inputs for the content
worker.

`workspace import` in ContentTool resolves locally, sends one authenticated
request to create the batch, then starts it. The PowerShell wrapper only invokes
this workflow and contains no import business rules.

## Consequences

- A catalog operation survives API or worker restarts and can resume from item
  checkpoints.
- Failures and retries are isolated per problem.
- Content-hash skip avoids unnecessary revisions and jobs.
- Bulk publication has an explicit review and audit boundary.
- PostgreSQL gains batch, item, and audit tables plus links from generation jobs
  and revisions.
- Large requests are internal-only, Admin-authorized, item/source bounded, and
  capped at the HTTP and application layers.
- This decision adds no public API and does not change learner catalogue
  visibility: only Published problems remain public.

## Alternatives considered

- Put batch logic in PowerShell: rejected because retries, invariants, and audit
  would not share the application workflow.
- Compile source in the API request: rejected because it couples request
  capacity to hostile compilation and bypasses worker leases.
- Create a new revision on every retry: rejected because retries must be
  idempotent.
- Publish every Ready item implicitly: rejected because approval must identify
  exact revisions.
- Add a separate message broker: rejected because PostgreSQL already provides
  the durable leased queue required for the MVP.
