# Roadmap

## Phase 0 — Scope and design baseline

**Goal:** approve the MVP contract before implementation changes.

- Review and approve the documents in `docs/`.
- Decide the frontend technology and deployment target through ADRs.
- Define the initial problem-package format and prepare 5-10 representative
  problems.
- Agree on the C++17-only boundary and the absence of scoring/Teacher roles.

**Exit criteria:** requirements, domain model, judge policy, and non-goals are
approved; implementation work can be checked against them.

## Phase 1 — Backend scope reset

**Goal:** turn the current API into the correct product core.

- Remove `Teacher`, user-selectable `Role`, `Score`, score leaderboard, and
  public problem/testcase authoring endpoints.
- Replace problem ownership with internal content operations.
- Add problem publication state, slug, tags, samples, and submission ownership
  checks.
- Close hidden testcase exposure and all related authorization gaps.
- Add consistent error handling, configuration validation, health checks, and
  a documented local environment.
- Adopt the public contract naming rules in `api-conventions.md` as endpoint
  contracts are redesigned.

**Exit criteria:** the API exposes only the intended public product surface and
has tests for authorization and sensitive-data boundaries.

## Phase 2 — Reliable judging

**Goal:** make Accepted trustworthy.

- Split the grading worker from the web API process.
- Implement atomic claims, lease recovery, idempotent finalization, and bounded
  retries.
- Correct runtime and memory measurement.
- Harden the sandbox and pin the compiler image.
- Add judge integration tests for every verdict.

**Exit criteria:** two workers cannot grade the same submission concurrently,
and all verdict paths pass automated tests in a Docker-capable environment.

## Phase 3 — Web MVP

**Goal:** deliver the complete learner practice loop.

- Build authentication, problem list, problem detail/editor, submit status,
  submission detail, history, and solved status.
- Support responsive desktop-first UI and accessible loading/error states.
- Add client-side API integration, token/session handling, and rate-limit UX.

**Exit criteria:** a new user can register, solve a seeded problem, receive an
Accepted verdict, and find the attempt in history.

## Phase 4 — Release readiness

**Goal:** safely run a closed beta.

- Add CI for formatting, build, test, dependency vulnerabilities, and
  integration tests.
- Ship Compose/deployment configuration, secrets guidance, backups, monitoring,
  and runbooks.
- Load test catalogue reads, submission creation, and queue behaviour.
- Perform a sandbox security review before exposing the service publicly.

**Exit criteria:** an operator can deploy, monitor, back up, and recover the
platform using documented procedures.

## Post-MVP candidates

Only prioritize these after the MVP is stable:

1. Custom-input **Run Code** against samples or explicit user input.
2. Additional languages through separate compiler/runtime adapters.
3. Problem lists, progress tracking, and a solved-count profile.
4. Contests and rankings based on an explicitly designed rule set.
5. Discussion, editorial content, and moderation.
