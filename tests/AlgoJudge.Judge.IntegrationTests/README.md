# AlgoJudge.Judge.IntegrationTests

Docker-backed tests cover Accepted, Wrong Answer, TLE, MLE, Compile Error,
Runtime Error, bounded output, and the runtime isolation policy.

Build the image and opt in locally:

```powershell
./scripts/build-judge-image.ps1
$env:TEST_DOCKER_JUDGE_IMAGE = "algojudge/judge-cpp17:14.3.0-v1"
dotnet test tests/AlgoJudge.Judge.IntegrationTests
```

Tests are skipped when `TEST_DOCKER_JUDGE_IMAGE` is absent. Backend CI builds
the image and supplies the variable, so a missing Docker daemon or broken image
fails the dedicated judge job rather than being silently skipped.
