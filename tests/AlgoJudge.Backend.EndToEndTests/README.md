# Backend end-to-end acceptance tests

This suite proves the deployed backend boundaries together rather than replacing
the narrower API, PostgreSQL repository, or Docker judge integration tests. It
uses a migrated temporary PostgreSQL database, the API test host, the real
`GraderWorker`, and the pinned C++17 Docker sandbox.

Coverage includes:

- register, login, browse, submit, queue claim, Docker judging, polling,
  history, and solved state;
- Accepted, Wrong Answer, Time Limit Exceeded, Memory Limit Exceeded, Compile
  Error, and Runtime Error;
- stdin/stdout and Function execution through the API, PostgreSQL queues, real
  worker, generated Function harness, and Docker sandbox;
- custom Run output stays separate from system-suite Submit history and solved
  state, while submissions remain pinned to their system-suite version;
- submission ownership and absence of source or hidden testcase data from API
  responses and captured normal logs;
- expired-lease recovery with stale-worker fencing; and
- two concurrent workers compiling and running one submission only once.

Run the complete suite locally with Docker Engine available:

```powershell
./scripts/test-backend-e2e.ps1
```

The script starts the test PostgreSQL Compose service when necessary, builds the
pinned judge image, sets the two test environment variables, and cleans up the
database service if it started it. Pass `-SkipImageBuild` only when the exact
versioned image already exists locally.

Tests skip when `TEST_POSTGRES_CONNECTION` or `TEST_DOCKER_JUDGE_IMAGE` is absent
so ordinary builds remain usable. The dedicated CI job provides both
prerequisites; a skip there is therefore a configuration failure, not an
accepted result.
