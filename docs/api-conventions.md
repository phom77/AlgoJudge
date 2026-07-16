# API Contract Conventions

## Contract type naming

Public API contract types must communicate their direction and purpose without
requiring the reader to inspect a controller signature.

| Purpose | Suffix | Example |
|---|---|---|
| Request body sent by a client | `Request` | `CreateSubmissionRequest` |
| Response returned by the API | `Response` | `SubmissionResponse` |
| Query-string filters and pagination | `Query` | `SubmissionHistoryQuery` |
| Internal application data transfer only | `Dto` | `JudgeExecutionDto` |
| Application command, when CQRS is introduced | `Command` | `SubmitSolutionCommand` |
| Application query, when CQRS is introduced | `Query` | `GetProblemDetailQuery` |

Do not combine redundant suffixes such as `RequestDto` or `ResponseDto`.

## Adoption status

The MVP authentication, problem catalogue, and submission endpoints use these
directional names. `Dto` is reserved for data that never crosses the public API
boundary. This convention applies to class names and file names.

Public collection endpoints return `PagedResponse<T>`. Repository pagination
uses an internal application `PagedResult<T>` model and is never returned
directly by a controller.

## Contract regression workflow

The approved v1 document is stored at
`tests/AlgoJudge.Api.IntegrationTests/Snapshots/openapi-v1.json`. The API test
suite generates `/openapi/v1.json`, removes environment-specific root server
URLs, canonicalizes object and semantic collection ordering, and compares the
result with this snapshot.

An intentional contract change must update the relevant API documentation and
be reviewed before running `./scripts/update-openapi-snapshot.ps1`. The snapshot
diff is part of the change; CI must never update it automatically. Unintentional
route, method, parameter, response, schema, enum, or security changes fail the
test suite.

ADR-0009 records the intentional final pre-frontend reset that replaced JSON
Bearer-token responses with secure cookie sessions. The resulting snapshot is
the baseline for the first generated Angular client. Any later breaking change
requires a new API document version.
