using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoJudge.Backend.EndToEndTests;

[Collection(BackendEndToEndCollection.Name)]
public sealed class BackendAcceptanceTests
{
    private const string HiddenInputSentinel = "123456789";
    private const string HiddenOutputSentinel = "246913578";
    private const string SourceSentinel = "private-source-e2e-sentinel-84d19c";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const string AcceptedSource = """
        // private-source-e2e-sentinel-84d19c
        #include <iostream>
        int main() {
            long long value = 0;
            std::cin >> value;
            std::cout << value * 2 << '\n';
        }
        """;

    private const string FunctionSignature =
        "{\"className\":\"Solution\",\"methodName\":\"solve\"," +
        "\"returnType\":\"Int32\",\"parameters\":[{" +
        "\"name\":\"value\",\"type\":\"Int32\"}]}";

    private const string FunctionAdapter = """
        #include <iostream>
        #include <iterator>
        #include <string>
        {{USER_SOURCE}}
        int main() {
            std::string input(
                (std::istreambuf_iterator<char>(std::cin)),
                std::istreambuf_iterator<char>());
            auto colon = input.find(':');
            int value = std::stoi(input.substr(colon + 1));
            {{CLASS_NAME}} solution;
            std::cout << solution.{{METHOD_NAME}}(value);
            return 0;
        }
        """;

    private const string FunctionSource =
        "class Solution { public: int solve(int value) { return value * 2; } };";

    [BackendEndToEndFact]
    public async Task FullUserFlowProducesEveryVerdictAndProtectsPrivateData()
    {
        await using var database = await EndToEndPostgreSqlDatabase.CreateAsync();
        var logs = new CapturingLoggerProvider();
        await using var factory = new EndToEndApiFactory(database.ConnectionString, logs);
        using var client = CreateClient(factory);
        var problem = await SeedProblemAsync(database);
        var timeLimitProblem = await SeedProblemAsync(
            database,
            timeLimitMs: 200,
            name: "time-limit");
        var memoryLimitProblem = await SeedProblemAsync(
            database,
            timeLimitMs: 5_000,
            name: "memory-limit");

        var anonymousCatalogue = await GetJsonAsync<PagedResponse<ProblemListItemResponse>>(
            client,
            $"/api/problems?search={problem.Slug}");
        var anonymousProblem = Assert.Single(anonymousCatalogue.Items);
        Assert.Null(anonymousProblem.IsSolved);

        var anonymousDetailResponse = await client.GetAsync($"/api/problems/{problem.Slug}");
        anonymousDetailResponse.EnsureSuccessStatusCode();
        var anonymousDetailJson = await anonymousDetailResponse.Content.ReadAsStringAsync();
        AssertPrivateDataAbsent(anonymousDetailJson);
        var anonymousDetail = Deserialize<ProblemDetailResponse>(anonymousDetailJson);
        Assert.Null(anonymousDetail.IsSolved);
        Assert.Equal("2", Assert.Single(anonymousDetail.Samples).Input.Trim());

        _ = await RegisterAndLoginAsync(client, "owner");

        var unsolvedDetail = await GetJsonAsync<ProblemDetailResponse>(
            client,
            $"/api/problems/{problem.Slug}");
        Assert.False(unsolvedDetail.IsSolved);

        var expectedVerdicts = new Dictionary<Guid, SubmissionStatus>();
        await AddSubmissionAsync(
            client,
            problem.Id,
            AcceptedSource,
            SubmissionStatus.Accepted,
            expectedVerdicts);
        await AddSubmissionAsync(
            client,
            problem.Id,
            "int main() { return 0; }",
            SubmissionStatus.WrongAnswer,
            expectedVerdicts);
        await AddSubmissionAsync(
            client,
            timeLimitProblem.Id,
            "int main() { while (true) { } }",
            SubmissionStatus.TimeLimitExceeded,
            expectedVerdicts);
        await AddSubmissionAsync(
            client,
            memoryLimitProblem.Id,
            MemoryLimitSource,
            SubmissionStatus.MemoryLimitExceeded,
            expectedVerdicts);
        await AddSubmissionAsync(
            client,
            problem.Id,
            "int main( { this is not valid C++; }",
            SubmissionStatus.CompileError,
            expectedVerdicts);
        await AddSubmissionAsync(
            client,
            problem.Id,
            "#include <cstdlib>\nint main() { std::abort(); }",
            SubmissionStatus.RuntimeError,
            expectedVerdicts);

        await using (var worker = await EndToEndWorkerHost.StartAsync(
                         database.ConnectionString,
                         "acceptance-worker",
                         logs))
        {
            foreach (var expected in expectedVerdicts)
            {
                var finalSubmission = await WaitForFinalSubmissionAsync(
                    client,
                    expected.Key);
                Assert.Equal(expected.Value, finalSubmission.Status);
            }
        }

        var historyResponse = await client.GetAsync("/api/submissions?pageSize=100");
        historyResponse.EnsureSuccessStatusCode();
        var historyJson = await historyResponse.Content.ReadAsStringAsync();
        AssertPrivateDataAbsent(historyJson);
        var history = Deserialize<PagedResponse<SubmissionResponse>>(historyJson);
        Assert.Equal(expectedVerdicts.Count, history.TotalCount);
        Assert.Equal(
            expectedVerdicts.Values.OrderBy(status => status),
            history.Items.Select(item => item.Status).OrderBy(status => status));

        var acceptedHistory = await GetJsonAsync<PagedResponse<SubmissionResponse>>(
            client,
            $"/api/submissions?problemId={problem.Id}&status=Accepted&pageSize=100");
        Assert.Single(acceptedHistory.Items);

        var solvedDetail = await GetJsonAsync<ProblemDetailResponse>(
            client,
            $"/api/problems/{problem.Slug}");
        Assert.True(solvedDetail.IsSolved);
        var solvedCatalogue = await GetJsonAsync<PagedResponse<ProblemListItemResponse>>(
            client,
            $"/api/problems?search={problem.Slug}&solved=true");
        Assert.True(Assert.Single(solvedCatalogue.Items).IsSolved);

        using var otherClient = CreateClient(factory);
        _ = await RegisterAndLoginAsync(otherClient, "other");
        var submissionId = expectedVerdicts.Keys.First();
        var forbiddenResponse = await otherClient.GetAsync(
            $"/api/submissions/{submissionId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        AssertPrivateDataAbsent(await forbiddenResponse.Content.ReadAsStringAsync());

        var otherHistoryResponse = await otherClient.GetAsync(
            "/api/submissions?pageSize=100");
        otherHistoryResponse.EnsureSuccessStatusCode();
        var otherHistoryJson = await otherHistoryResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(
            submissionId.ToString(),
            otherHistoryJson,
            StringComparison.OrdinalIgnoreCase);
        AssertPrivateDataAbsent(otherHistoryJson);

        var combinedLogs = string.Join(Environment.NewLine, logs.Entries);
        AssertPrivateDataAbsent(combinedLogs);
    }

    [BackendEndToEndFact]
    public async Task CustomRunsAndFunctionSubmissionsKeepTheirExecutionBoundaries()
    {
        await using var database = await EndToEndPostgreSqlDatabase.CreateAsync();
        var logs = new CapturingLoggerProvider();
        await using var factory = new EndToEndApiFactory(database.ConnectionString, logs);
        using var client = CreateClient(factory);
        var stdinProblem = await SeedProblemAsync(database, name: "custom-run");
        var functionProblem = await SeedFunctionProblemAsync(database);
        _ = await RegisterAndLoginAsync(client, "execution");

        await using var worker = await EndToEndWorkerHost.StartAsync(
            database.ConnectionString,
            "execution-acceptance-worker",
            logs);

        var stdinRun = await CreateRunAsync(
            client,
            stdinProblem.Slug,
            new { sourceCode = AcceptedSource, language = "cpp17", input = "21\n" });
        var completedStdinRun = await WaitForFinalRunAsync(client, stdinRun.Id);
        Assert.Equal(RunStatus.Completed, completedStdinRun.Status);
        Assert.Equal("42", completedStdinRun.Stdout?.Trim());

        var emptyHistory = await GetJsonAsync<PagedResponse<SubmissionResponse>>(
            client,
            "/api/submissions?pageSize=100");
        Assert.Empty(emptyHistory.Items);
        Assert.False((await GetJsonAsync<ProblemDetailResponse>(
            client,
            $"/api/problems/{stdinProblem.Slug}")).IsSolved);

        var functionRun = await CreateRunAsync(
            client,
            functionProblem.Slug,
            new
            {
                sourceCode = FunctionSource,
                language = "cpp17",
                arguments = new { value = 21 }
            });
        var completedFunctionRun = await WaitForFinalRunAsync(client, functionRun.Id);
        Assert.Equal(RunStatus.Completed, completedFunctionRun.Status);
        Assert.Equal("42", completedFunctionRun.Stdout?.Trim());

        var submission = await SubmitAsync(client, functionProblem.Id, FunctionSource);
        Assert.Equal(1, submission.SystemTestSuiteVersion);
        var accepted = await WaitForFinalSubmissionAsync(client, submission.Id);
        Assert.Equal(SubmissionStatus.Accepted, accepted.Status);

        var history = await GetJsonAsync<PagedResponse<SubmissionResponse>>(
            client,
            "/api/submissions?pageSize=100");
        Assert.Equal(submission.Id, Assert.Single(history.Items).Id);
        Assert.True((await GetJsonAsync<ProblemDetailResponse>(
            client,
            $"/api/problems/{functionProblem.Slug}")).IsSolved);

        await using var context = database.CreateContext();
        Assert.Equal(2, await context.CodeRuns.CountAsync());
        Assert.Equal(1, await context.Submissions.CountAsync());
        Assert.Equal(1, await context.Submissions.Select(item =>
            item.SystemTestSuiteVersion).SingleAsync());

        AssertPrivateDataAbsent(string.Join(Environment.NewLine, logs.Entries));
    }

    [BackendEndToEndFact]
    public async Task TwoWorkersCompileAndJudgeOneSubmissionOnlyOnce()
    {
        await using var database = await EndToEndPostgreSqlDatabase.CreateAsync();
        var logs = new CapturingLoggerProvider();
        await using var factory = new EndToEndApiFactory(database.ConnectionString, logs);
        using var client = CreateClient(factory);
        var problem = await SeedProblemAsync(database);
        _ = await RegisterAndLoginAsync(client, "workers");
        var created = await SubmitAsync(client, problem.Id, AcceptedSource);
        var sandbox = new CountingDockerSandbox(
            EndToEndWorkerHost.CreateDockerSandbox());

        await using (var firstWorker = await EndToEndWorkerHost.StartAsync(
                         database.ConnectionString,
                         "acceptance-worker-a",
                         logs,
                         sandbox))
        await using (var secondWorker = await EndToEndWorkerHost.StartAsync(
                         database.ConnectionString,
                         "acceptance-worker-b",
                         logs,
                         sandbox))
        {
            var finalSubmission = await WaitForFinalSubmissionAsync(client, created.Id);
            Assert.Equal(SubmissionStatus.Accepted, finalSubmission.Status);
        }

        Assert.Equal(1, sandbox.CompileCount);
        Assert.Equal(1, sandbox.RunCount);
        await using var context = database.CreateContext();
        var persisted = await context.Submissions.AsNoTracking()
            .SingleAsync(submission => submission.Id == created.Id);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(SubmissionStatus.Accepted, persisted.Status);
        Assert.Null(persisted.WorkerId);
        Assert.Null(persisted.ClaimToken);
    }

    [BackendEndToEndFact]
    public async Task ExpiredCrashedWorkerLeaseIsRecoveredAndFenced()
    {
        await using var database = await EndToEndPostgreSqlDatabase.CreateAsync();
        var logs = new CapturingLoggerProvider();
        await using var factory = new EndToEndApiFactory(database.ConnectionString, logs);
        using var client = CreateClient(factory);
        var problem = await SeedProblemAsync(database);
        _ = await RegisterAndLoginAsync(client, "recovery");
        var created = await SubmitAsync(client, problem.Id, AcceptedSource);

        await using var crashedContext = database.CreateContext();
        var crashedRepository = new SubmissionRepository(crashedContext);
        var staleClaim = await crashedRepository.ClaimNextAsync(
            "crashed-worker",
            TimeSpan.FromMilliseconds(250),
            maxAttempts: 3);
        Assert.NotNull(staleClaim);
        Assert.Equal(created.Id, staleClaim!.SubmissionId);
        await Task.Delay(600);

        var sandbox = new CountingDockerSandbox(
            EndToEndWorkerHost.CreateDockerSandbox());
        await using (var recoveryWorker = await EndToEndWorkerHost.StartAsync(
                         database.ConnectionString,
                         "recovery-worker",
                         logs,
                         sandbox))
        {
            var finalSubmission = await WaitForFinalSubmissionAsync(client, created.Id);
            Assert.Equal(SubmissionStatus.Accepted, finalSubmission.Status);
        }

        Assert.Equal(1, sandbox.CompileCount);
        Assert.False(await crashedRepository.FinalizeClaimAsync(
            staleClaim,
            SubmissionStatus.WrongAnswer,
            executionTimeMs: 1,
            memoryUsedKb: 1));

        await using var verificationContext = database.CreateContext();
        var persisted = await verificationContext.Submissions.AsNoTracking()
            .SingleAsync(submission => submission.Id == created.Id);
        Assert.Equal(2, persisted.AttemptCount);
        Assert.Equal(SubmissionStatus.Accepted, persisted.Status);
    }

    private static async Task AddSubmissionAsync(
        HttpClient client,
        int problemId,
        string sourceCode,
        SubmissionStatus expectedStatus,
        IDictionary<Guid, SubmissionStatus> expectedVerdicts)
    {
        var submission = await SubmitAsync(client, problemId, sourceCode);
        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        expectedVerdicts.Add(submission.Id, expectedStatus);
    }

    private static async Task<SubmissionResponse> SubmitAsync(
        HttpClient client,
        int problemId,
        string sourceCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/submissions",
            new
            {
                problemId,
                sourceCode,
                language = "cpp17"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        AssertPrivateDataAbsent(json);
        return Deserialize<SubmissionResponse>(json);
    }

    private static async Task<RunResponse> CreateRunAsync(
        HttpClient client,
        string problemSlug,
        object request)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/problems/{problemSlug}/runs",
            request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        AssertPrivateDataAbsent(json);
        return Deserialize<RunResponse>(json);
    }

    private static async Task<RunResponse> WaitForFinalRunAsync(
        HttpClient client,
        Guid runId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        while (true)
        {
            var response = await client.GetAsync($"/api/runs/{runId}", timeout.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            AssertPrivateDataAbsent(json);
            var run = Deserialize<RunResponse>(json);
            if (run.Status is not (RunStatus.Pending or RunStatus.Running))
                return run;

            await Task.Delay(200, timeout.Token);
        }
    }

    private static async Task<SubmissionResponse> WaitForFinalSubmissionAsync(
        HttpClient client,
        Guid submissionId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        while (true)
        {
            var response = await client.GetAsync(
                $"/api/submissions/{submissionId}",
                timeout.Token);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(timeout.Token);
            AssertPrivateDataAbsent(json);
            var submission = Deserialize<SubmissionResponse>(json);
            if (submission.Status is not (
                    SubmissionStatus.Pending or SubmissionStatus.Running))
            {
                return submission;
            }

            await Task.Delay(200, timeout.Token);
        }
    }

    private static async Task<AuthResponse> RegisterAndLoginAsync(
        HttpClient client,
        string prefix)
    {
        await EnableAntiforgeryAsync(client);
        var unique = Guid.NewGuid().ToString("N");
        var userName = $"{prefix}_{unique}";
        const string password = "test-password-123";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName,
                email = $"{prefix}_{unique}@example.test",
                password,
                fullName = $"{prefix} user"
            });
        await AssertSuccessAsync(registerResponse, "register test account");

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName, password });
        await AssertSuccessAsync(loginResponse, "login test account");
        return Deserialize<AuthResponse>(
            await loginResponse.Content.ReadAsStringAsync());
    }

    private static async Task EnableAntiforgeryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var cookieHeader = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        var encodedToken = cookieHeader.Split(';', 2)[0].Split('=', 2)[1];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add(
            "X-XSRF-TOKEN",
            Uri.UnescapeDataString(encodedToken));
    }

    private static async Task AssertSuccessAsync(
        HttpResponseMessage response,
        string operation)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Could not {operation}. HTTP {(int)response.StatusCode}: {responseBody}");
    }

    private static async Task<Problem> SeedProblemAsync(
        EndToEndPostgreSqlDatabase database,
        int timeLimitMs = 1_000,
        string name = "workflow")
    {
        await using var context = database.CreateContext();
        var problem = new Problem
        {
            Slug = $"backend-e2e-{name}-{Guid.NewGuid():N}",
            Title = $"Backend end-to-end acceptance {name}",
            StatementMarkdown = "Double the supplied integer.",
            ConstraintsMarkdown = "The input fits in a signed 64-bit integer.",
            TimeLimitMs = timeLimitMs,
            MemoryLimitKb = 64 * 1024,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        problem.Samples.Add(new ProblemSample
        {
            Input = "2\n",
            ExpectedOutput = "4\n",
            Explanation = "Two doubled is four.",
            Ordinal = 1
        });
        problem.JudgeTestCases.Add(new JudgeTestCase
        {
            Input = HiddenInputSentinel + "\n",
            ExpectedOutput = HiddenOutputSentinel + "\n",
            Ordinal = 1
        });
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        return problem;
    }

    private static async Task<Problem> SeedFunctionProblemAsync(
        EndToEndPostgreSqlDatabase database)
    {
        await using var context = database.CreateContext();
        var problem = new Problem
        {
            Slug = $"backend-e2e-function-{Guid.NewGuid():N}",
            Title = "Backend end-to-end function acceptance",
            StatementMarkdown = "Double the supplied function argument.",
            ConstraintsMarkdown = "The value fits in a signed 32-bit integer.",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 64 * 1024,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow,
            ExecutionMode = ProblemExecutionMode.Function,
            FunctionSignatureJson = FunctionSignature,
            FunctionAdapterTemplate = FunctionAdapter
        };
        problem.Samples.Add(new ProblemSample
        {
            Input = "{\"value\":2}",
            ExpectedOutput = "4",
            Explanation = "Two doubled is four.",
            Ordinal = 1
        });
        problem.JudgeTestCases.Add(new JudgeTestCase
        {
            Input = $"{{\"value\":{HiddenInputSentinel}}}",
            ExpectedOutput = HiddenOutputSentinel,
            SystemTestSuiteVersion = 1,
            Ordinal = 1
        });
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        return problem;
    }

    private static async Task<T> GetJsonAsync<T>(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        AssertPrivateDataAbsent(json);
        return Deserialize<T>(json);
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Could not deserialize {typeof(T).Name} from the API response.");
    }

    private static void AssertPrivateDataAbsent(string value)
    {
        Assert.DoesNotContain(HiddenInputSentinel, value, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenOutputSentinel, value, StringComparison.Ordinal);
        Assert.DoesNotContain(SourceSentinel, value, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(EndToEndApiFactory factory)
    {
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private const string MemoryLimitSource = """
        #include <cstddef>
        #include <cstdlib>
        int main() {
            constexpr std::size_t bytes = 256ULL * 1024ULL * 1024ULL;
            volatile unsigned char* memory =
                static_cast<volatile unsigned char*>(std::malloc(bytes));
            if (memory == nullptr) {
                while (true) { }
            }
            for (std::size_t index = 0; index < bytes; index += 4096) {
                memory[index] = 1;
            }
            return memory[0];
        }
        """;
}
