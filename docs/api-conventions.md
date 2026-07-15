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

## Adoption plan

The scope-reset change intentionally keeps the existing `*Dto` class names to
avoid mixing a mechanical rename with domain removal. Starting with the Problem
Catalogue API contract, new public types use `Request`, `Response`, and `Query`.
Existing public DTOs are renamed when their endpoint contract is redesigned.

This convention applies to class names and file names.
