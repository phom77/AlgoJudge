# AlgoJudge Content Worker

This host claims PostgreSQL content-generation jobs and orchestrates the pinned
C# generator and C++17 judge sandboxes. The API only persists snapshots and
status; it never compiles or runs authoring source.

The host requires access to the Docker CLI/Engine, but containers it creates do
not receive the Docker socket.

```powershell
./scripts/build-content-generator-image.ps1
./scripts/build-judge-image.ps1
./scripts/run-content-worker.ps1
```

Claims use renewable leases, unique fencing tokens, bounded retries, and
conditional completion. Logs contain job identifiers and safe categories only.
