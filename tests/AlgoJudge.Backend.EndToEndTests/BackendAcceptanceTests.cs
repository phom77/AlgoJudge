using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
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

        var account = await RegisterAndLoginAsync(client, "acceptance-owner");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", account.AccessToken);

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

        var otherAccount = await RegisterAndLoginAsync(client, "acceptance-other");
        var submissionId = expectedVerdicts.Keys.First();
        using var forbiddenRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/submissions/{submissionId}",
            otherAccount.AccessToken);
        var forbiddenResponse = await client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        AssertPrivateDataAbsent(await forbiddenResponse.Content.ReadAsStringAsync());

        using var otherHistoryRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/submissions?pageSize=100",
            otherAccount.AccessToken);
        var otherHistoryResponse = await client.SendAsync(otherHistoryRequest);
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
    public async Task TwoWorkersCompileAndJudgeOneSubmissionOnlyOnce()
    {
        await using var database = await EndToEndPostgreSqlDatabase.CreateAsync();
        var logs = new CapturingLoggerProvider();
        await using var factory = new EndToEndApiFactory(database.ConnectionString, logs);
        using var client = CreateClient(factory);
        var problem = await SeedProblemAsync(database);
        var account = await RegisterAndLoginAsync(client, "two-workers");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", account.AccessToken);
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
        var account = await RegisterAndLoginAsync(client, "lease-recovery");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", account.AccessToken);
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
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName, password });
        loginResponse.EnsureSuccessStatusCode();
        return Deserialize<AuthResponse>(
            await loginResponse.Content.ReadAsStringAsync());
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

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken);
        return request;
    }

    private static HttpClient CreateClient(EndToEndApiFactory factory)
    {
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
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
