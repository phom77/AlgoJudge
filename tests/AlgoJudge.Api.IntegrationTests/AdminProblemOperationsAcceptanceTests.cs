using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AlgoJudge.API.Security;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AlgoJudge.Api.IntegrationTests;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AdminProblemOperationsAcceptanceTests
{
    private const string HiddenInput = "admin-hidden-input-sentinel";
    private const string HiddenOutput = "admin-hidden-output-sentinel";

    [PostgreSqlFact]
    public async Task AdminLifecycleArchivesAndRestoresWithoutExposingHiddenTestcases()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        var problemId = await SeedPublishedProblemAsync(factory);
        using var admin = CreateClient(factory, UserRole.Admin);
        await ApiTestClientSecurity.EnableAntiforgeryAsync(admin);

        var listed = await admin.GetAsync("/api/internal/admin/problems");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var listedJson = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(HiddenInput, listedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenOutput, listedJson, StringComparison.Ordinal);

        var archive = await admin.PostAsync($"/api/internal/admin/problems/{problemId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        await AssertStatusAsync(factory, problemId, ProblemStatus.Archived);

        var restore = await admin.PostAsync($"/api/internal/admin/problems/{problemId}/restore", null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);
        await AssertStatusAsync(factory, problemId, ProblemStatus.Published);
    }

    [PostgreSqlFact]
    public async Task RegularUserCannotReadOrChangeAdminProblemOperations()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        var problemId = await SeedPublishedProblemAsync(factory);
        using var user = CreateClient(factory, UserRole.User);
        await ApiTestClientSecurity.EnableAntiforgeryAsync(user);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/internal/admin/problems")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.PostAsync($"/api/internal/admin/problems/{problemId}/archive", null)).StatusCode);
    }

    private static async Task<int> SeedPublishedProblemAsync(AlgoJudgeApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var problem = new Problem
        {
            Slug = $"admin-acceptance-{Guid.NewGuid():N}",
            Title = "Admin operations acceptance",
            StatementMarkdown = "Public statement",
            ConstraintsMarkdown = "Public constraints",
            TimeLimitMs = 1_000,
            MemoryLimitKb = 262_144,
            Difficulty = DifficultyLevel.Easy,
            Status = ProblemStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        problem.SystemTestSuites.Add(new PublishedSystemTestSuite { Version = 1 });
        problem.JudgeTestCases.Add(new JudgeTestCase
        {
            Input = HiddenInput,
            ExpectedOutput = HiddenOutput,
            Ordinal = 1
        });
        context.Problems.Add(problem);
        await context.SaveChangesAsync();
        return problem.Id;
    }

    private static async Task AssertStatusAsync(
        AlgoJudgeApiFactory factory, int problemId, ProblemStatus expected)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(expected, await context.Problems.Where(problem => problem.Id == problemId)
            .Select(problem => problem.Status).SingleAsync());
    }

    private static HttpClient CreateClient(AlgoJudgeApiFactory factory, UserRole role)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("Cookie", $"{AuthCookieManager.AccessCookieName}={CreateToken(role)}");
        return client;
    }

    private static string CreateToken(UserRole role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-secret-key-at-least-32-characters"));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "AlgoJudge.IntegrationTests", audience: "AlgoJudge.IntegrationTests.Client", claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }
}
