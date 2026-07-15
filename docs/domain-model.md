# Domain Model

## 1. Core entities

```mermaid
erDiagram
    USER ||--o{ SUBMISSION : creates
    PROBLEM ||--o{ SUBMISSION : receives
    PROBLEM ||--o{ TEST_CASE : contains
    PROBLEM }o--o{ TAG : has
```

### User

Represents a regular platform account.

| Field | Notes |
|---|---|
| `id` | Immutable UUID primary key. |
| `username` | Unique display and login name. |
| `email` | Unique login/contact identifier. |
| `passwordHash` | Never exposed by any API. |
| `fullName` | Optional display name policy may be decided later. |
| `createdAt` | UTC timestamp. |

There is no `Role` field in the MVP domain.

### Problem

Represents a curated programming exercise.

| Field | Notes |
|---|---|
| `id` | Internal numeric or UUID identifier. |
| `slug` | Unique, stable URL identifier, for example `two-sum`. |
| `title` | User-facing title. |
| `statementMarkdown` | Problem description rendered safely by the client. |
| `constraintsMarkdown` | Input and output constraints. |
| `difficulty` | `Easy`, `Medium`, or `Hard`. |
| `timeLimitMs` | Runtime limit used by the judge. |
| `memoryLimitKb` | Memory limit used by the judge. |
| `status` | `Draft`, `Published`, or `Archived`. |
| `publishedAt` | UTC timestamp when applicable. |
| `createdAt`, `updatedAt` | UTC audit timestamps. |

`Score` and `CreatedBy` are intentionally excluded from the public product
model. Content provenance, if needed, belongs to internal operations metadata.

### TestCase

Represents judge-only input and expected output.

| Field | Notes |
|---|---|
| `id` | Internal identifier; never returned to a normal user. |
| `problemId` | Parent problem. |
| `input` | Exact stdin content. |
| `expectedOutput` | Canonical expected stdout. |
| `ordinal` | Stable execution order. |
| `isSample` | True only when mirrored in the public statement. |

The public API returns sample data embedded in the Problem representation, not
generic TestCase records. All judge test data must be treated as confidential.

### Submission

Represents one immutable code attempt.

| Field | Notes |
|---|---|
| `id` | UUID primary key. |
| `userId` | The owner. |
| `problemId` | The submitted problem. |
| `language` | MVP allows only `cpp17`. |
| `sourceCode` | Immutable submitted source. |
| `status` | Lifecycle state described below. |
| `executionTimeMs` | Max observed testcase execution time, if known. |
| `memoryUsedKb` | Max observed testcase memory use, if known. |
| `compileMessage` | Sanitized and size-limited; visible only to owner. |
| `createdAt`, `startedAt`, `finishedAt` | UTC lifecycle timestamps. |
| `attemptCount` | Operational retry count, not user-visible scoring. |

### Tag

Represents a reusable classification such as `array`, `dynamic-programming`, or
`binary-search`. A tag has a unique slug and display name.

## 2. Submission state machine

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Running: atomic worker claim
    Running --> Accepted
    Running --> WrongAnswer
    Running --> TimeLimitExceeded
    Running --> MemoryLimitExceeded
    Running --> CompileError
    Running --> RuntimeError
```

Final states are immutable. A worker crash before a final state is handled by a
lease/timeout recovery policy, which returns the job to Pending or marks it as
an operational failure after a bounded retry count.

## 3. Domain invariants

- Only a Published problem accepts new submissions.
- A submission belongs to exactly one user and one problem.
- A user can only read their own submission source and diagnostics.
- `Accepted` means every testcase passed under the configured limits.
- A problem cannot be Published without valid samples and at least one hidden
  testcase.
- Hidden testcase content is never part of public DTOs or logs.
- Solved status is derived from submissions, not stored as an editable flag.
