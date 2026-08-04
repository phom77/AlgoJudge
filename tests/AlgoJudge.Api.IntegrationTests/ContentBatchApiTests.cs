using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AlgoJudge.Api.IntegrationTests;

[Collection(ApiIntegrationCollection.Name)]
public sealed class ContentBatchApiTests
{
    private const string PrivateSourceSentinel = "private-source-must-not-be-returned";

    [PostgreSqlFact]
    public async Task AdminCanCreateAndStartBatchWithoutPrivateSourcesInResponses()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        var adminId = await SeedUserAsync(factory, UserRole.Admin);
        using var admin = CreateBearerClient(factory, adminId, UserRole.Admin);

        var create = await admin.PostAsJsonAsync(
            "/api/internal/admin/content-batches",
            Request(),
            ApiJsonOptions);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createJson = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PrivateSourceSentinel, createJson, StringComparison.Ordinal);
        var batch = JsonSerializer.Deserialize<ContentBatchResponse>(
            createJson,
            ApiJsonOptions)!;

        var start = await admin.PostAsync(
            $"/api/internal/admin/content-batches/{batch.Id}/start",
            null);

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startJson = await start.Content.ReadAsStringAsync();
        Assert.DoesNotContain(PrivateSourceSentinel, startJson, StringComparison.Ordinal);
        Assert.DoesNotContain("definitionJson", startJson, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await context.ContentBatches.FindAsync(batch.Id);
        Assert.NotNull(persisted);
        Assert.Equal(ContentBatchStatus.Generating, persisted.Status);
        var persistedJob = await context.ContentGenerationJobs
            .SingleAsync(job => job.BatchItem!.BatchId == batch.Id);
        var persistedDefinition = ProblemAuthoringDefinitionJson.Deserialize(
            persistedJob.DefinitionSnapshotJson);
        Assert.Equal(
            ProblemAuthoringDefinitionJson.ComputeSha256(persistedDefinition),
            persistedJob.DefinitionSha256);
        var auditText = await context.ContentBatchAuditEntries
            .Where(entry => entry.BatchId == batch.Id)
            .Select(entry => entry.Action + ":" + entry.Result + ":" +
                             entry.SafeFailureCategory)
            .ToArrayAsync();
        Assert.All(auditText, value =>
            Assert.DoesNotContain(PrivateSourceSentinel, value, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task AnonymousAndRegularUsersCannotAccessBatchAdministration()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        using var anonymous = factory.CreateClient();
        var userId = await SeedUserAsync(factory, UserRole.User);
        using var user = CreateBearerClient(factory, userId, UserRole.User);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/internal/admin/content-batches")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/internal/admin/content-batches")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await user.PostAsJsonAsync(
                "/api/internal/admin/content-batches",
                Request(),
                ApiJsonOptions)).StatusCode);
    }

    private static CreateContentBatchRequest Request() => new()
    {
        CatalogName = "catalog.json",
        Items =
        [
            new CreateContentBatchItemRequest
            {
                CatalogPath = "problems/private/problem.json",
                Action = ContentBatchImportAction.Create,
                ContentHash = new string('a', 64),
                Slug = $"batch-api-{Guid.NewGuid():N}",
                Title = "Batch API",
                StatementMarkdown = "Statement",
                ConstraintsMarkdown = "Constraints",
                Difficulty = DifficultyLevel.Easy,
                Tags = ["arrays"],
                TimeLimitMs = 1_000,
                MemoryLimitKb = 262_144,
                Samples =
                [
                    new ProblemSampleRequest
                    {
                        Input = "{\"values\":[1]}",
                        ExpectedOutput = "1"
                    }
                ],
                GeneratorParameters = JsonSerializer.SerializeToElement(new { count = 10 }),
                Definition = new ProblemAuthoringDefinition
                {
                    SchemaVersion = 1,
                    ExecutionMode = ProblemExecutionMode.Function,
                    FunctionSignature = new FunctionSignature
                    {
                        ClassName = "Solution",
                        MethodName = "solve",
                        ReturnType = FunctionValueType.Int32,
                        Parameters =
                        [
                            new FunctionParameter
                            {
                                Name = "values",
                                Type = FunctionValueType.Int32Array
                            }
                        ]
                    },
                    HandwrittenCases =
                    [
                        new HandwrittenCaseDefinition
                        {
                            Name = "sample",
                            Group = "handwritten",
                            Arguments = JsonSerializer.SerializeToElement(
                                new { values = new[] { 1 } })
                        }
                    ],
                    Generator = new GeneratorSourceDefinition
                    {
                        Language = "csharp",
                        SdkVersion = 1,
                        Source = PrivateSourceSentinel
                    },
                    InputValidator = new GeneratorSourceDefinition
                    {
                        Language = "csharp",
                        SdkVersion = 1,
                        Source = PrivateSourceSentinel
                    },
                    ReferenceSolution = new FunctionSourceDefinition
                    {
                        Language = "cpp17",
                        Source = PrivateSourceSentinel
                    }
                }
            }
        ]
    };

    private static async Task<Guid> SeedUserAsync(
        AlgoJudgeApiFactory factory,
        UserRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = id,
            UserName = $"batch_{id:N}",
            Email = $"{id:N}@example.test",
            FullName = "Batch Admin",
            PasswordHash = "test",
            Role = role
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static HttpClient CreateBearerClient(
        AlgoJudgeApiFactory factory,
        Guid userId,
        UserRole role)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(userId, role));
        return client;
    }

    private static string CreateToken(Guid userId, UserRole role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                "integration-test-secret-key-at-least-32-characters"));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "AlgoJudge.IntegrationTests",
            audience: "AlgoJudge.IntegrationTests.Client",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256)));
    }

    private static readonly JsonSerializerOptions ApiJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(allowIntegerValues: false)
            }
        };
}
