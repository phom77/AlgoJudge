# Product Requirements

## 1. Definitions

- **Problem:** a published programming exercise with a statement, examples,
  limits, and a private test suite.
- **Sample:** an input/output pair displayed in the problem statement.
- **Hidden test case:** judge-only input and expected output that is never
  returned by the public API.
- **Submission:** one immutable attempt by a user to solve one problem.
- **Execution mode:** either a complete stdin/stdout program or a declared
  class method executed through a private system harness.
- **Verdict:** the final or intermediate result of judging a submission.
- **Solved:** a user has at least one Accepted submission for a problem.
- **Authoring revision:** an internal, versioned definition from which a
  maintainer generates and reviews one candidate system suite.
- **System suite:** an immutable, positive-versioned collection of hidden
  testcase input/output pairs and its generation provenance.

## 2. Functional requirements

### Accounts and sessions

| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-01 | A visitor can create a standard account. | Registration always creates a regular user; the request cannot choose a privileged role. Duplicate username/email is rejected. |
| FR-02 | A user can log in and refresh a session. | Passwords are stored only as secure hashes. Access and refresh tokens follow the configured expiry policy. |
| FR-03 | A user can revoke their active refresh token. | A revoked token cannot create another access token. |

### Problem catalogue

| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-10 | Anyone can list published problems. | Results are paginated and include title, slug, difficulty, tags, and the caller's solved state when authenticated. Draft problems are absent. |
| FR-11 | Users can search and filter the catalogue. | Search matches title and slug; filters support difficulty, tags, and solved/unsolved status for an authenticated user. |
| FR-12 | Anyone can view a published problem. | The response includes the statement, constraints, public samples, limits, and metadata. It never includes hidden test content or expected output. |
| FR-13 | A user can see whether they have solved a problem. | The status is derived from at least one Accepted submission, not from a mutable client flag. |

### Submission and judging

| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-20 | An authenticated user can submit code for a published problem. | The API accepts only a supported language and source code within the configured size limit. StdinStdout accepts a complete program; Function accepts the declared class/method implementation. The created submission starts as Pending. |
| FR-21 | The system judges every accepted submission exactly once. | A worker claims Pending work atomically before execution. Retrying a crashed job must not produce two final results or switch the system-suite version captured at submission creation. |
| FR-22 | The user can read the state of their own submission. | The state changes from Pending to Running and then to one final verdict. The user sees compile diagnostics only for their own submission. |
| FR-23 | A submission is Accepted only if every testcase in its pinned published system suite passes. | The judge executes cases in stable order and stops at the first failure for MVP. It records an internal failure summary but does not reveal hidden input or output. |
| FR-24 | A user can view their submission history. | History is paginated and filterable by problem and verdict. A user cannot read another user's history or submission detail. |
| FR-25 | The system protects capacity from accidental or abusive submission bursts. | Per-user and per-IP rate limits are enforced and return a clear retry response. |
| FR-26 | An authenticated user can run code with custom input for a published problem. | The run is queued, owner-only, and executes once without reading hidden testcases or changing submission history and solved state. StdinStdout accepts raw input; Function accepts arguments matching the declared signature. |
| FR-27 | A user can read the state and bounded output of their own custom run. | The response excludes source and input. Successful execution is Completed rather than Accepted; compile diagnostics, stdout, and stderr remain owner-only and bounded. |

### Content operations

| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-30 | Maintainers can import a problem package through an internal tool. | The tool validates statement metadata, samples, testcase pairs, total uncompressed size, and duplicate names before persisting anything. |
| FR-31 | Maintainers can publish or unpublish a problem. | Only published problems can be listed, viewed, or submitted to by public users. |
| FR-32 | Hidden tests remain private. | There is no public endpoint, query flag, log entry, or error message that exposes hidden input or expected output. |
| FR-33 | Maintainers can define a Function problem without building a project, DLL, full stdin/stdout program, or per-problem adapter. | One `ProblemAuthoringDefinition` contains a validated function signature, handwritten arguments, generator and validator source, a reference class/method, and optional wrong solutions. Generator helpers do not restrict problem-specific logic to a fixed strategy registry. |
| FR-34 | The system generates candidate suites offline from a reproducible snapshot. | A content worker compiles and runs authoring source outside the API and grading worker, derives every seed, validates all arguments, creates outputs with the reference solution, repeats the run to detect non-determinism, and hashes the complete provenance. |
| FR-35 | Maintainers can review a generated suite before publishing it. | An authoring revision follows Draft, Generating, Ready, and Published; failures safely return to Draft. Only an explicit publish of a Ready candidate assigns a new positive immutable suite version. |
| FR-36 | Published suite versions remain immutable. | Editing a Ready or Published definition creates or returns to a Draft revision, never changes an existing suite, and never changes the suite version pinned by an existing submission. |
| FR-37 | Legacy content remains usable during authoring migration. | Package schema versions 1 and 2, their imported tests, and existing schema-version-2 private adapters continue to import and judge without conversion. |
| FR-38 | Internal maintainers can manage source-authored revisions through a protected backend workflow. | Configured maintainers can edit only owned revisions, enqueue generation, inspect safe status and aggregate review statistics, and explicitly publish a Ready candidate. Internal responses never expose candidate input/output. |

## 3. Required verdicts

| Verdict | Meaning |
|---|---|
| Pending | Submission is persisted and awaits a worker claim. |
| Running | A worker owns the submission and is compiling or executing it. |
| Accepted | Compilation succeeded and every testcase passed within limits. |
| WrongAnswer | Program completed but produced incorrect output. |
| TimeLimitExceeded | Program exceeded the problem's execution limit. |
| MemoryLimitExceeded | Program exceeded the problem's memory limit. |
| CompileError | Source code could not be compiled. |
| RuntimeError | Program exited abnormally or the runner failed after a safe retry policy. |

## 4. Data validation rules

- Usernames: 3-50 ASCII letters, digits, or underscores; unique case-insensitively.
- Emails: valid format; unique case-insensitively.
- Source code: required and limited to a configured maximum size; MVP default is
  64 KiB.
- A problem title, slug, statement, limits, difficulty, and at least one hidden
  testcase are required before publishing.
- Time and memory limits must be positive and bounded by platform configuration.
- A testcase archive has limits for compressed size, uncompressed size, file
  count, and individual file size.
- Schema-version-1 problem packages import as StdinStdout. Schema version 2
  explicitly declares StdinStdout or Function.
- Function packages require a validated signature, private adapter template,
  and JSON sample/hidden-test values matching the declared parameter and return
  types.
- Source-based authoring version 1 requires Function mode, a validated
  signature, at least one handwritten case, bounded generator/validator and
  reference source, and values matching the declared parameter and return
  types. The platform generates its private harness; maintainers do not supply
  an adapter.

## 5. API-level error behaviour

- Invalid client input returns a stable validation response without stack traces.
- Authentication failures return `401`; ownership failures return `403`.
- Missing public resources return `404` without revealing whether a draft or
  private resource exists.
- Submission creation returns `429` when rate-limited.
- Judge infrastructure faults must be observable internally but appear as a
  safe Runtime Error or retriable operational state to the user.
