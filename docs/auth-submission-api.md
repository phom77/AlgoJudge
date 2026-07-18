# Authentication and Submission API

This document defines the stable MVP authentication, submission, and custom-run contracts.
All JSON property names use camel case. Timestamps are UTC ISO 8601 values and
enums are serialized as strings.

## Authentication

### Register

`POST /api/auth/register`

The request contains `userName`, `email`, `password`, and `fullName`.
Registration returns `200 OK` with an `AuthResponse`. A duplicate username or
email returns `409 Conflict`. Access and refresh credentials are issued only as
secure HttpOnly cookies and never appear in the JSON response.

### Login

`POST /api/auth/login`

The request contains `userName` and `password`. Valid credentials return
`200 OK` with an `AuthResponse` and replace the credential cookies; invalid
credentials return `401 Unauthorized`.

### Browser session and antiforgery

- `GET /api/auth/csrf` issues the antiforgery cookies required before an unsafe
  cookie-authenticated request. Angular reads `XSRF-TOKEN` and sends it in the
  `X-XSRF-TOKEN` header.
- `GET /api/auth/session` requires the access cookie and returns the current
  `AuthResponse` without credentials.
- `POST /api/auth/refresh` reads and rotates the HttpOnly refresh cookie. It has
  no request body and returns a new `AuthResponse` with `200 OK`.
- `POST /api/auth/revoke` reads the HttpOnly credential cookies, revokes the
  refresh token, deletes both cookies, and returns `204 No Content`. It has no
  request body.

`AuthResponse` contains only `userName`, `email`, and `expiresAt`. The access
cookie is host-only with path `/`; the refresh cookie is restricted to
`/api/auth`. Both are `HttpOnly`, `Secure`, `SameSite=Strict`, and omit the
`Domain` attribute. The SPA and API must use one origin; local Angular
development uses an API proxy.

All unsafe `/api` requests authenticated by cookies require antiforgery
validation. Missing or invalid antiforgery state returns `403` with code
`csrf`. Machine clients that explicitly send a Bearer header remain supported
and are not subject to browser-cookie antiforgery validation.

## Submissions

Every submission endpoint requires the secure access cookie (or an explicit
Bearer credential for a non-browser client).

### Create a submission

`POST /api/submissions`

The request contains:

| Property | Contract |
|---|---|
| `problemId` | Required positive integer identifying a published problem. |
| `sourceCode` | Required; maximum 65,536 UTF-8 bytes. |
| `language` | Required and must be exactly `cpp17`. |

A valid request returns `201 Created`, a `Location` header for the new
submission, and a `SubmissionResponse` whose initial status is `Pending`.

### Get submission detail

`GET /api/submissions/{id}`

The response contains `id`, `problemId`, `language`, `status`,
`executionTimeMs`, `memoryUsedKb`, `createdAt`, `startedAt`, and `finishedAt`.
The user ID is intentionally absent because ownership is enforced by the API.
The response includes `systemTestSuiteVersion`, the non-sensitive published
judge version captured when the submission was created. It never includes
hidden testcase identifiers, inputs, or expected outputs.

The database lookup includes both submission ID and authenticated user ID, so a
non-owner's source and operational fields are never materialized by the API.
An existing submission owned by another user returns `403 Forbidden`; an
unknown submission returns `404 Not Found`.

### Create a custom run

`POST /api/problems/{slug}/runs`

The authenticated request contains `sourceCode`, `language: "cpp17"`, and
either `input` for a StdinStdout problem or an `arguments` JSON object for a
Function problem. It returns `201 Created`, a Location header for the run, and
a Pending `RunResponse`. Input is limited to 64 KiB of UTF-8 data.

### Get custom-run detail

`GET /api/runs/{id}`

Only the owner can read a run. The response contains status, bounded `stdout`
and `stderr`, execution time, peak memory, and timestamps; it never returns
source code or input. An existing run owned by another user returns `403
Forbidden`; an unknown run returns `404 Not Found`. Runs do not appear in
submission history and never affect solved state.

### Get submission history

`GET /api/submissions`

| Parameter | Type | Default | Validation |
|---|---|---|---|
| `problemId` | integer | none | Must be greater than zero. |
| `status` | submission status | none | Must be a defined enum name. |
| `pageNumber` | integer | `1` | Must be at least 1. |
| `pageSize` | integer | `20` | Must be between 1 and 100. |

The endpoint returns `PagedResponse<SubmissionResponse>` with `items`,
`totalCount`, `pageNumber`, `pageSize`, and `totalPages`. Invalid pagination is
rejected with `400 Bad Request`; the API never silently substitutes defaults
for invalid supplied values.

## Error contract

Errors use `application/problem+json` and contain the RFC 7807 fields `type`,
`title`, `status`, `detail`, and `instance`, plus stable `code` and `traceId`
fields. Validation responses additionally contain an `errors` dictionary.

The stable error codes are `validation`, `authentication`, `forbidden`, `csrf`,
`not-found`, `conflict`, `rate-limit`, and `internal`. Error responses never
contain stack traces, secrets, source code, or hidden testcase content.

History queries derive their user scope exclusively from the authenticated
token. There is no public user ID filter. Problem responses expose public
samples but never load or serialize private `JudgeTestCase` rows.
