# ADR-0006: Use versioned ZIP problem packages

Status: Accepted
Date: 2026-07-15

## Context

Maintainers need a repeatable way to validate and import problem statements,
samples, tags, and private judge cases without creating a public authoring API.
The format must be portable, reviewable in a private content repository, and
safe to process even when an archive is malformed or hostile.

## Decision

Use a ZIP archive with a strict, versioned manifest and fixed directory layout.
Schema version 1 contains `problem.json`, Markdown statement files, ordered
public sample pairs, and ordered private test pairs.

ContentTool reads entries directly from the archive and never extracts them to
the filesystem. It rejects path traversal, duplicate or unexpected names,
invalid UTF-8, incomplete pairs, and configured size/count limit violations
before opening a database transaction.

Import creates a Draft problem. An existing slug is rejected by default. The
explicit `--replace` option may replace only an existing Draft and increments
its judge version. Published and Archived problems cannot be overwritten.

## Consequences

- Hidden tests remain outside the public API and public content repository.
- Package validation can run without a database connection.
- Import is atomic and cannot persist a partially validated package.
- Future incompatible formats require a new schema version and reader path.
- Publishing remains a separate operation and is not implied by import.

## Alternatives considered

- Directory-only packages: convenient locally but do not provide a bounded,
  portable import artifact.
- A single JSON document containing all test data: rejected because large test
  content becomes difficult to review, pair, and validate independently.
- Public administration endpoints: rejected because content operations are an
  internal maintainer concern for the MVP.
