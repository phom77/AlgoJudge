# Internal Problem Authoring API

These endpoints support the maintainer UI and are intentionally excluded from
`/openapi/v1.json`. They require a valid session, membership in the configured
`MaintainerAccess:UserIds` allowlist, antiforgery protection for unsafe methods,
and ownership of the target revision.

Base route: `/api/internal/admin/problem-drafts`

| Method and route | Purpose |
|---|---|
| `POST /` | Create a Function problem and its first Draft revision. |
| `POST /problems/{problemId}/revisions` | Create the next Draft from an owned Published revision. |
| `GET /{revisionId}` | Read owned metadata and authoring definition. |
| `PUT /{revisionId}/metadata` | Save metadata and public samples. |
| `PUT /{revisionId}/signature` | Save the Function signature. |
| `PUT /{revisionId}/handwritten-cases` | Replace handwritten cases. |
| `PUT /{revisionId}/sources` | Save generator, validator, reference, and wrong-solution source. |
| `POST /{revisionId}/generation` | Snapshot the Draft and enqueue generation. |
| `GET /{revisionId}/generation` | Read job state and safe errors. |
| `GET /{revisionId}/suite-review` | Read counts by group, differential kill counts, survivors, hashes, and toolchain identity. |
| `POST /{revisionId}/publish` | Publish a Ready candidate atomically. |

Editing Ready deletes its private candidate and returns it to Draft. Generating
snapshots and Published revisions are immutable. Suite review never includes
generated input or expected output. Compiler output, source, generated values,
and reference output are absent from job status and normal logs.
