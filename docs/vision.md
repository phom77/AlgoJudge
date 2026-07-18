# AlgoJudge Product Vision

## 1. Product statement

AlgoJudge is a focused online programming practice platform. A user reads an
algorithmic problem, writes a solution in the browser, submits it, and receives
an objective verdict from an isolated judge.

The MVP is inspired by the core practice loop of LeetCode, not by its full
feature set. A submission is **Accepted** only when it passes every test case
within the problem limits. The product does not award numeric scores.

## 2. Target users

- **Learner:** practices data structures and algorithms, wants clear problems
  and fast, trustworthy feedback.
- **Maintainer:** prepares and publishes the curated problem catalogue and its
  hidden tests. This is an operational responsibility, not a public Teacher
  role in the MVP.

## 3. Core user promise

"I can practice a problem, submit code safely, and trust that Accepted means my
solution passed the complete test suite."

## 4. MVP boundaries

### In scope

- Account registration and login.
- Public problem browsing, search, filtering, and problem detail pages.
- Curated, published problems with Markdown statements, examples, tags,
  difficulty, and resource limits.
- C++17 code submission and asynchronous judging.
- C++17 custom-input runs through the same asynchronous sandbox, without
  affecting submission history or solved state.
- Curated problems may accept either a complete stdin/stdout program or a
  declared C++17 class method through a private system harness.
- Standard verdicts: Pending, Running, Accepted, Wrong Answer, Time Limit
  Exceeded, Memory Limit Exceeded, Compile Error, and Runtime Error.
- Submission history and per-user solved status.
- An internal, non-public way to import and publish problem content and test
  cases.

### Explicitly out of scope for MVP

- Teacher, student, or public author roles.
- Numeric scoring, score-based leaderboards, ratings, or achievements.
- Contests, teams, discussion forums, comments, and social features.
- Multiple programming languages.
- Payments, certificates, plagiarism detection, and mobile applications.

## 5. Product principles

1. **Trust the verdict.** Judge correctness and isolation matter more than
   feature breadth.
2. **Keep the learning loop short.** Browsing, submitting, and seeing a
   verdict must be straightforward.
3. **Hide implementation details.** Hidden test data, expected output, and
   sandbox internals are never exposed to users.
4. **Curate before scaling content.** A small, well-tested catalogue is more
   valuable than many inconsistent problems.
5. **Earn complexity.** Add languages, contests, and custom runs only after
   the single-language submission flow is reliable.

## 6. MVP success criteria

The MVP is ready for a small closed beta when:

- a new user can complete the browse-to-accepted flow without manual support;
- every published problem has verified samples and hidden tests;
- all supported verdict paths have automated tests;
- no public endpoint can read or modify hidden test data;
- duplicate grading cannot occur when more than one worker is running; and
- deployment, rollback, database migration, and local setup are documented.
