using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AlgoJudge.Api.IntegrationTests;

[Collection(ApiIntegrationCollection.Name)]
public class SubmissionSecurityTests
{
    private const string SourceSentinel = "private-source-sentinel-71e23f";
    private const string HiddenInputSentinel = "hidden-input-sentinel-26bd1a";
    private const string HiddenOutputSentinel = "hidden-output-sentinel-9c204e";

    [PostgreSqlFact]
    public async Task SubmissionAndProblemReadsEnforceOwnerAndHiddenDataBoundaries()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        var ownerAuth = await RegisterAsync(client, "owner");
        var otherAuth = await RegisterAsync(client, "other");
        var seeded = await SeedSubmissionAsync(factory, ownerAuth.UserName);

        using var ownerDetail = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/submissions/{seeded.SubmissionId}",
            ownerAuth.AccessToken);
        var ownerResponse = await client.SendAsync(ownerDetail);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var ownerJson = await ownerResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SourceSentinel, ownerJson, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenInputSentinel, ownerJson, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenOutputSentinel, ownerJson, StringComparison.Ordinal);

        using var deniedDetail = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/submissions/{seeded.SubmissionId}",
            otherAuth.AccessToken);
        var deniedResponse = await client.SendAsync(deniedDetail);
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        var deniedJson = await deniedResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SourceSentinel, deniedJson, StringComparison.Ordinal);

        using var missingDetail = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/submissions/{Guid.NewGuid()}",
            otherAuth.AccessToken);
        var missingResponse = await client.SendAsync(missingDetail);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var otherHistory = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/submissions",
            otherAuth.AccessToken);
        var historyResponse = await client.SendAsync(otherHistory);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.DoesNotContain(
            seeded.SubmissionId.ToString(),
            await historyResponse.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        var problemResponse = await client.GetAsync($"/api/problems/{seeded.ProblemSlug}");
        Assert.Equal(HttpStatusCode.OK, problemResponse.StatusCode);
        var problemJson = await problemResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(HiddenInputSentinel, problemJson, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenOutputSentinel, problemJson, StringComparison.Ordinal);
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string prefix)
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName = $"{prefix}_{unique}",
                email = $"{prefix}_{unique}@example.test",
                password = "test-password-123",
                fullName = $"{prefix} user"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<(Guid SubmissionId, string ProblemSlug)> SeedSubmissionAsync(
        AlgoJudgeApiFactory factory,
        string ownerUserName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await context.Users.SingleAsync(user =>
            user.UserName == ownerUserName);
        var problem = new Problem
        {
            Slug = $"security-{Guid.NewGuid():N}",
            Title = "Security boundary test",
            StatementMarkdown = "Statement",
            ConstraintsMarkdown = "Constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        problem.JudgeTestCases.Add(new JudgeTestCase
        {
            Input = HiddenInputSentinel,
            ExpectedOutput = HiddenOutputSentinel,
            Ordinal = 1
        });
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            User = owner,
            Problem = problem,
            SourceCode = SourceSentinel,
            Language = "cpp17",
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.AddRange(problem, submission);
        await context.SaveChangesAsync();
        return (submission.Id, problem.Slug);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static HttpClient CreateClient(AlgoJudgeApiFactory factory)
    {
        return factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }
}
