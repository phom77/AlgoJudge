# Internal Problem Authoring API

These endpoints support the administrator UI and are intentionally excluded from
`/openapi/v1.json`. They require a valid Admin session, antiforgery protection
for unsafe methods, and ownership of the target revision.

Base route: `/api/internal/admin/problem-drafts`

The same-origin maintainer client is generated from
`/openapi/admin-v1.json`. This internal document contains only authoring routes
and is versioned separately so the stable learner contract remains unchanged.

| Method and route | Purpose |
|---|---|
| `POST /` | Create a Function problem and its first Draft revision. |
| `POST /problems/{problemId}/revisions` | Create the next Draft from an owned Published revision. |
| `GET /{revisionId}` | Read owned metadata and authoring definition. |
| `PUT /{revisionId}/metadata` | Save metadata and public samples. |
| `PUT /{revisionId}/signature` | Save the Function signature. |
| `PUT /{revisionId}/handwritten-cases` | Replace handwritten cases. |
| `PUT /{revisionId}/sources` | Save generator, validator, reference, and wrong-solution source. |
| `PUT /{revisionId}/quality-policy` | Save minimum total/group coverage and wrong-solution-kill requirements. |
| `POST /{revisionId}/generation` | Snapshot the Draft and enqueue generation. |
| `GET /{revisionId}/generation` | Read job state and safe errors. |
| `GET /{revisionId}/suite-review` | Read counts by group, differential kill counts, survivors, hashes, and toolchain identity. |
| `POST /{revisionId}/publish` | Publish a Ready candidate atomically. |

Editing Ready deletes its private candidate and returns it to Draft. Generating
snapshots and Published revisions are immutable. Suite review never includes
generated input or expected output. Compiler output, source, generated values,
and reference output are absent from job status and normal logs.

`qualityPolicy` belongs to the revision definition and is validated before a
generation job is created. A candidate that misses a required total, group, or
declared wrong-solution kill fails with the safe `quality_gate_failed` job
category. Publish repeats the policy check over the stored private candidate;
it cannot publish a Ready revision that no longer satisfies its snapshot.

The suite review includes at most 100 safe case metadata rows: ordinal, name,
group, deterministic seed, and killed wrong-solution identifiers. A truncation
flag tells the UI when more cases exist. Existing ZIP packages continue through
the audited `AlgoJudge.ContentTool` import workflow; the browser does not upload
private legacy packages.
