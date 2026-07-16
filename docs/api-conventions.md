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
