# Judge Specification

## 1. Purpose

The judge compiles and executes untrusted user code for a submission or an
owner-scoped custom run. Its
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
  problem signature. For source-authored problems the platform builds a
  complete source file with its generic C++17 harness. Legacy schema-version-2
  content continues to use its private adapter template. Test input and
  expected output are normalized JSON matching the signature.

The generated harness and legacy Function adapters are trusted private
platform content, but combined source still compiles and executes under the
same sandbox boundaries as any other submission. Harness/adapter source,
signature internals beyond the public method contract, and hidden
arguments/output must not appear in logs or diagnostics.

The persisted compatibility rule is explicit: a Function problem with a
legacy adapter uses that adapter; a Function problem with no adapter uses the
generic harness generated from its required signature. StdinStdout problems
cannot retain either a signature or adapter. Existing adapters are not
rewritten.

The generic C++17 mapping is:

| Signature type | C++17 type |
|---|---|
| `Int32` | `int` |
| `Int64` | `long long` |
| `Double` | `double` |
| `Boolean` | `bool` |
| `String` | `std::string` |
| `*Array` | `std::vector<T>` using the corresponding scalar mapping |

The harness strictly parses one JSON object, requires exactly the declared
parameter names, invokes the method in signature order, and emits one compact
JSON value. It supports JSON escapes and Unicode surrogate pairs, rejects
out-of-range integers and non-finite doubles, and escapes returned strings.
Malformed arguments, a thrown solution exception, or an unserializable result
exit abnormally and therefore map to Runtime Error. The same generic builder
is exposed to offline reference execution so learner and oracle methods use
identical parsing and serialization.

## 3. Judge algorithm

1. A worker atomically claims a Pending submission and selects the exact
   system-suite version pinned when that submission was created.
2. It selects the problem execution mode. For Function problems it builds the
   C++17 harness; for StdinStdout it uses the submitted source unchanged.
3. It writes the resulting source code to a unique ephemeral work directory.
4. It compiles the source inside an isolated compiler container.
5. On compiler failure, it stores a sanitized and truncated diagnostic and
   finalizes as Compile Error.
6. On success, it starts one hardened runtime container for the selected suite.
   A trusted native batch supervisor invokes the existing native runner once
   per testcase in stable ordinal order, so every testcase receives a fresh
   solution process while container startup is amortized across the suite.
7. For each process, the native runner independently captures exit status,
   bounded stdout/stderr, elapsed time, and peak memory.
8. The supervisor stops immediately after a process, resource, or output-limit
   failure. Successful outputs return in framed order for host-side checker
   comparison; because comparison happens outside the sandbox, later successful
   processes in that batch may already have run when an earlier output is
   classified as Wrong Answer. The worker still reports the first failing
   testcase in stable ordinal order and finalizes one verdict.
9. It deletes the work directory and all temporary artifacts.

System suites are selected by problem ID plus positive suite version and are
executed in stable ordinal order. The same immutable suite record selects its
output checker. Missing versions are operational failures, not empty Accepted
suites. Generator code and reference solutions never run in the worker.
Submission retries and reclaimed leases use the original pinned version.
Submission testcase inputs use a length-framed batch protocol over runtime
container stdin. Content-generation protocol requests and custom-run input also
use container stdin. All are UTF-8 without a byte-order mark on every supported
host OS. Hidden testcase files are never mounted into the runtime container.

## 3.1 Offline content generation

Content generation has a separate trust and process boundary from submission
judging. The API and grading worker never compile or execute generator,
validator, reference, or wrong-solution source. A separately deployable content
worker, or the transitional CLI using the same adapters, orchestrates pinned
content-generation sandboxes.

Generator and validator work is separate from C++17 reference work, and
compilation is separate from execution for each toolchain. All stages use
non-root containers with no network, dropped capabilities, no new privileges,
read-only runtime filesystems, bounded scratch mounts and explicit CPU, time,
memory, PID, file, and stdout/stderr limits. Containers receive no database
credentials, Docker socket, application source, host devices, or home
directory.

Generation uses an immutable authoring snapshot. It validates every argument,
uses the generic Function harness for the reference method, repeats generation
and reference execution from identical seeds, and rejects byte-level
non-determinism. Optional wrong solutions run under the same C++17 sandbox.
The reference harness is compiled once and executed in two ordered runtime
batches for the determinism comparison. Each wrong solution is compiled once
and executed in one ordered batch that continues after per-case failures so
differential coverage includes the complete suite. Every case still runs in a
fresh measured process with independent resource limits.
Only a complete, hashed candidate can become a positive immutable system-suite
version. Generated source, hidden values, and raw diagnostics are excluded from
normal logs and public contracts.

For a custom run, the same compile and sandbox stages execute exactly once with
the user's bounded input. The worker does not load hidden testcases or compare
expected output. A successful process is `Completed`; other terminal states are
Time Limit Exceeded, Memory Limit Exceeded, Compile Error, or Runtime Error.
The run queue has independent claim tokens and leases, while the worker
alternates queue priority and executes only one claim at a time.

## 4. Output comparison

Every immutable system-suite version selects one platform-owned checker. A
checker receives only bounded expected and contestant output after normal
sandbox verdict handling; it never receives or executes author-provided code.

| Checker | Behaviour |
|---|---|
| `TokenExact` | Split expected and actual output on Unicode whitespace and compare token sequences exactly. Case and numeric text are preserved. |
| `JsonExact` | Parse one JSON value from each output and compare its JSON structure. Whitespace and object-property order do not affect equality; malformed output is Wrong Answer. |
| `FloatingPoint` | Split on Unicode whitespace. Each corresponding token must be a finite invariant-culture floating-point value and satisfy either the configured absolute or relative tolerance. |

Only `FloatingPoint` stores tolerances. Both values are finite and non-negative,
and at least one is positive. A suite checker is persisted with its version;
publishing a later suite is the only way to change comparison semantics for new
submissions. Source-authored Function suites use `JsonExact`; legacy suites and
schema-version-1/2 packages use `TokenExact` for compatibility.

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
writable root filesystem. A system-suite submission uses one runtime container
with a trusted batch supervisor, but every testcase still runs as a separate
measured solution process with per-case limits. Custom runs remain single-case
runtime containers.

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
