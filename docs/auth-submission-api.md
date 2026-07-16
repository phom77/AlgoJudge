# Authentication and Submission API

This document defines the stable MVP authentication and submission contracts.
All JSON property names use camel case. Timestamps are UTC ISO 8601 values and
enums are serialized as strings.

## Authentication

### Register

`POST /api/auth/register`

The request contains `userName`, `email`, `password`, and `fullName`.
Registration returns `200 OK` with an `AuthResponse`. A duplicate username or
email returns `409 Conflict`.

### Login

`POST /api/auth/login`

The request contains `userName` and `password`. Valid credentials return
`200 OK` with an `AuthResponse`; invalid credentials return `401 Unauthorized`.

### Refresh and revoke

- `POST /api/auth/refresh` accepts a `refreshToken` and returns a new
  `AuthResponse` with `200 OK`.
- `POST /api/auth/revoke` requires bearer authentication, accepts a
  `refreshToken`, and returns `204 No Content`.

`AuthResponse` contains `accessToken`, `refreshToken`, `tokenType`, `userName`,
`email`, and `expiresAt`. `tokenType` is `Bearer`.

## Submissions

Every submission endpoint requires bearer authentication.

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

The stable error codes are `validation`, `authentication`, `forbidden`,
`not-found`, `conflict`, `rate-limit`, and `internal`. Error responses never
contain stack traces, secrets, source code, or hidden testcase content.
