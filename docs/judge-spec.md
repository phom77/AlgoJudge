# Judge Specification

## 1. Purpose

The judge compiles and executes untrusted user code for a submission. Its
primary responsibilities are verdict correctness, resource enforcement, and
isolation from the application and host infrastructure.

## 2. Supported language in MVP

| Property | Value |
|---|---|
| API language value | `cpp17` |
| Compiler | `g++` with C++17 enabled |
| Optimisation | `-O2` |
| Compiler image | AlgoJudge image based on pinned `gcc:14.3.0-bookworm` digest |
| Source limit | 64 KiB by default; configurable |

The API rejects every other language. The database and judge adapter should be
designed so another language can be added later without changing submission
semantics.

## 2.1 Problem execution modes

- `StdinStdout`: the submitted source is a complete C++17 program. Test input
  is passed to stdin and program stdout is compared with expected output.
- `Function`: the submitted source defines the class and method declared by the
  problem signature. The judge builds a complete source file from the private
  adapter template, user source, class name, and method name. Test input and
  expected output are normalized JSON matching the signature.

Function adapters are trusted private problem content, but the combined source
still compiles and executes under the same sandbox boundaries as any other
submission. Adapter source, signature internals beyond the public method
contract, and hidden arguments/output must not appear in logs or diagnostics.

## 3. Judge algorithm

1. A worker atomically claims a Pending submission.
2. It selects the problem execution mode. For Function problems it builds the
   C++17 harness; for StdinStdout it uses the submitted source unchanged.
3. It writes the resulting source code to a unique ephemeral work directory.
4. It compiles the source inside an isolated compiler container.
5. On compiler failure, it stores a sanitized and truncated diagnostic and
   finalizes as Compile Error.
6. On success, it executes the binary once per testcase in stable ordinal
   order.
7. For each run, it captures exit status, bounded stdout/stderr, elapsed time,
   and peak memory.
8. It stops at the first failure in MVP and finalizes one verdict.
9. It deletes the work directory and all temporary artifacts.

## 4. Output comparison

For MVP, output comparison is token-based:

- split expected and actual output on Unicode whitespace;
- compare the resulting token sequences exactly;
- treat trailing spaces and trailing newlines as insignificant;
- preserve case and numeric text exactly.

Problems that need a floating-point tolerance are out of scope until an
explicit per-problem comparator is designed. The comparator used for a problem
must be recorded with its test suite version.

## 5. Resource enforcement

- Each problem declares a positive time limit in milliseconds and memory limit
  in KiB.
- CPU time is measured inside the execution environment; Docker startup time is
  not considered solution runtime.
- Wall time is measured by the in-container runner with a monotonic clock.
- Peak memory is reported from `wait4/getrusage`; Docker cgroup OOM state is a
  hard fallback for Memory Limit Exceeded.
- Memory enforcement and measurement must use a mechanism supported by the
  deployment OS. If peak memory cannot be measured reliably, MLE must not be
  claimed as implemented.
- The runner applies an outer watchdog timeout only as a fail-safe.
- Stdout and stderr are captured through independent bounded pipes. Exceeding
  either limit stops the program without growing worker memory without bound.
- Process count, file descriptors, core files, memory, and swap are bounded.

## 6. Sandbox requirements

Every execution container must have:

- no network access;
- a non-root user;
- no Linux capabilities beyond the explicit minimum, preferably `CAP_DROP=ALL`;
- `no-new-privileges` and a restrictive seccomp/AppArmor profile where the host
  supports it;
- a read-only root filesystem;
- only required temporary writable directories;
- a read-only mount of the compiled binary during execution;
- CPU, memory, process, file-descriptor, and output-size limits; and
- no Docker socket, host devices, home directory, or application source mount.

Compilation may need a writable isolated build directory. Execution must use a
separate, read-only artifact mount whenever practical.

Compilation and execution always use different containers. The compile stage
receives a writable build mount and bounded temporary filesystem. The runtime
stage receives only the compiled artifact through a read-only mount and has no
writable root filesystem.

## 7. Verdict mapping

| Observation | Verdict |
|---|---|
| Compiler returns non-zero | Compile Error |
| Process exceeds runtime watchdog | Time Limit Exceeded |
| Cgroup/resource enforcement reports memory excess | Memory Limit Exceeded |
| Stdout or stderr exceeds its configured byte limit | Runtime Error in MVP |
| Process returns non-zero, signal, or forbidden operation | Runtime Error |
| Process exits normally with different normalized output | Wrong Answer |
| Every testcase exits normally and output matches | Accepted |

Internal runner faults are logged with a correlation ID. Their raw details,
including hidden input/output, are not returned to the user.

## 8. Queue and recovery requirements

- PostgreSQL is the durable MVP queue; Redis and RabbitMQ are not required.
- Claiming work uses `FOR UPDATE SKIP LOCKED` and atomically changes either a
  `Pending` submission or an expired retryable `Running` submission to
  `Running`.
- Every claim has a worker ID, unique claim token, renewable lease, and attempt
  number. The claim token acts as a fencing token.
- A worker restart can recover expired leases without creating two active
  owners for one submission.
- Lease renewal, retry release, and final state updates are conditional on the
  submission ID, worker ID, claim token, and current state.
- A stale worker cannot finalize a submission after another worker reclaims it.
- Failed attempts return to `Pending` until the configured attempt limit is
  reached; an exhausted submission ends as `RuntimeError`.
- The system records enough internal metadata to investigate a judge failure
  without retaining sensitive temporary files.
