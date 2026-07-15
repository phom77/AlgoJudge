# User Flows

## 1. Browse a problem

1. A visitor opens the problem list.
2. The client requests published problems with optional search, difficulty, and
   tag filters.
3. The visitor selects a problem.
4. The problem page displays the statement, constraints, examples, and limits.
5. If authenticated, the page also displays whether the user has solved it.

**Important:** samples are public content. Hidden test cases are never loaded
by this page or its API calls.

## 2. Register and log in

1. The visitor submits username, email, full name, and password.
2. The API validates the values, creates a regular user, and returns a session.
3. The client stores tokens according to the selected security design.
4. On expiration, the client refreshes the session or prompts the user to log
   in again.

There is no role selector in the UI or public API.

## 3. Submit a solution

1. An authenticated user opens a published problem.
2. The user writes C++17 source code in the editor.
3. The user presses **Submit**.
4. The client validates that source code is non-empty and sends the problem ID,
   language, and source code.
5. The API verifies authentication, publication state, language, source size,
   and rate limits; it persists a Pending submission.
6. The client navigates to the submission detail page or polls its status.
7. A worker atomically claims the submission and changes it to Running.
8. The worker compiles and executes the code against hidden tests in the
   sandbox.
9. The client shows the final verdict and safe diagnostics.

## 4. View a submission result

1. The client fetches a submission that belongs to the current user.
2. If it is Pending or Running, the client shows a progress state and polls
   with bounded backoff.
3. On a final verdict, it shows execution time and memory use when available.
4. For Compile Error, it shows sanitized compiler output.
5. For Wrong Answer, TLE, MLE, and Runtime Error, it may show a generic
   explanation but never hidden input, expected output, or a hidden testcase ID.

## 5. View history and solved status

1. The authenticated user opens their submission history.
2. The client requests only that user's submissions, optionally filtered by
   problem or verdict.
3. A problem is marked Solved when at least one result is Accepted.
4. Repeated Accepted submissions do not increase a score because the MVP has no
   scoring system.

## 6. Maintain problem content (internal flow)

1. A maintainer prepares a versioned problem package outside the public app.
2. The import tool validates metadata, samples, and hidden testcase pairs.
3. The tool creates or updates a Draft problem.
4. A maintainer verifies samples locally and publishes the problem.
5. Publishing makes it visible and submittable; unpublishing blocks new
   submissions while retaining historical records.

The internal content tool is deliberately separate from the public product
surface in the MVP.
