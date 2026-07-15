# Problem Catalogue API

The problem catalogue is a public, read-only API. It exposes only problems with
`Published` status. Authentication is optional and adds user-specific solved
state when a valid access token is present.

## List published problems

`GET /api/problems`

### Query parameters

| Parameter | Type | Default | Behaviour |
|---|---|---|---|
| `search` | string | none | Case-insensitive match against title or slug. Maximum 100 characters. |
| `difficulty` | `Easy`, `Medium`, `Hard` | none | Exact difficulty filter. |
| `tags` | string, repeatable | none | Every supplied tag slug must belong to the problem. |
| `solved` | boolean | none | Filters by Accepted-submission history. Requires authentication. |
| `pageNumber` | integer | `1` | Must be at least 1. |
| `pageSize` | integer | `20` | Must be between 1 and 100. |

Example:

```http
GET /api/problems?search=array&difficulty=Easy&tags=array&tags=hash-table&pageNumber=1&pageSize=20
```

Each item contains `id`, `slug`, `title`, `difficulty`, `tags`, and `isSolved`.
For an anonymous caller, `isSolved` is `null`. For an authenticated caller, it
is derived from whether that user has at least one `Accepted` submission for the
problem. Supplying `solved` anonymously returns `400 Bad Request`.

## Get a published problem

`GET /api/problems/{slug}`

The response contains the statement and constraints in Markdown, resource
limits, difficulty, judge version, publication timestamp, tags, ordered public
samples, and optional solved state. A missing, Draft, or Archived problem
returns `404 Not Found`.

The response cannot contain `JudgeTestCase` data. Public samples live in a
separate table from private judge cases, and public repository queries never
include the private navigation.

## Submission relationship

`POST /api/submissions` continues to identify a problem by its internal numeric
ID. Submission creation now rejects Draft and Archived problems; only a
Published problem can create a Pending submission.
