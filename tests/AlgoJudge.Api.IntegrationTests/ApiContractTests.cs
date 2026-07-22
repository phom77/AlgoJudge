using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlgoJudge.Api.IntegrationTests;

public class ApiContractTests
{
    private const string UnusedConnectionString =
        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";

    [Fact]
    public async Task OpenApiV1MatchesApprovedSnapshot()
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);
        var openApiJson = await client.GetStringAsync("/openapi/v1.json");
        var actual = OpenApiSnapshot.Canonicalize(openApiJson);
        var snapshotPath = OpenApiSnapshot.GetSnapshotPath();

        if (Environment.GetEnvironmentVariable(
                OpenApiSnapshot.UpdateEnvironmentVariable) == "1")
        {
            Assert.False(
                string.Equals(
                    Environment.GetEnvironmentVariable("CI"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                "OpenAPI snapshots cannot be updated in CI.");
            await OpenApiSnapshot.WriteAsync(snapshotPath, actual);
            return;
        }

        Assert.True(
            File.Exists(snapshotPath),
            $"OpenAPI snapshot is missing at '{snapshotPath}'. " +
            "Run ./scripts/update-openapi-snapshot.ps1 to create it.");
        var expected = (await File.ReadAllTextAsync(snapshotPath))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "OpenAPI v1 differs from the approved semantic snapshot. " +
            "Review the generated contract and, only if intentional, run " +
            "./scripts/update-openapi-snapshot.ps1." + Environment.NewLine +
            OpenApiSnapshot.DescribeDifference(expected, actual));
    }

    [Fact]
    public async Task OpenApiUsesDirectionalContractNamesAndLowercaseRoutes()
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var openApiJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Dto", openApiJson, StringComparison.Ordinal);

        using var openApi = JsonDocument.Parse(openApiJson);
        var paths = openApi.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/auth/register", out _));
        Assert.True(paths.TryGetProperty("/api/problems", out _));
        Assert.True(paths.TryGetProperty("/api/submissions", out _));
        Assert.DoesNotContain(paths.EnumerateObject(), path =>
            path.Name.StartsWith("/api/internal/admin", StringComparison.Ordinal));

        var schemas = openApi.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("RegisterRequest", out _));
        Assert.True(schemas.TryGetProperty("AuthResponse", out _));
        Assert.True(schemas.TryGetProperty("CreateSubmissionRequest", out _));
        Assert.True(schemas.TryGetProperty("SubmissionResponse", out _));
        Assert.True(schemas.TryGetProperty("ApiProblemDetails", out _));
        Assert.True(schemas.TryGetProperty("ApiValidationProblemDetails", out _));
        Assert.DoesNotContain(
            schemas.EnumerateObject().Select(schema => schema.Name),
            name => name.Contains("JudgeTestCase", StringComparison.OrdinalIgnoreCase));

        var cookieScheme = openApi.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("CookieSession");
        Assert.Equal("apiKey", cookieScheme.GetProperty("type").GetString());
        Assert.Equal("cookie", cookieScheme.GetProperty("in").GetString());
        Assert.Equal(
            "__Host-algojudge-access",
            cookieScheme.GetProperty("name").GetString());

        AssertSecurityRequired(paths, "/api/submissions", "get", "CookieSession");
        AssertSecurityRequired(
            paths,
            "/api/submissions",
            "post",
            "CookieSession",
            "AntiforgeryHeader");
        AssertSecurityRequired(
            paths,
            "/api/auth/register",
            "post",
            "AntiforgeryHeader");
        AssertSecurityRequired(
            paths,
            "/api/auth/refresh",
            "post",
            "RefreshCookie",
            "AntiforgeryHeader");
        AssertSecurityRequired(
            paths,
            "/api/auth/revoke",
            "post",
            "CookieSession",
            "RefreshCookie",
            "AntiforgeryHeader");
        AssertSecurityRequired(
            paths,
            "/api/submissions/{id}",
            "get",
            "CookieSession");
        AssertOptionalCookie(paths, "/api/problems", "get");
        AssertOptionalCookie(paths, "/api/problems/{slug}", "get");
    }

    [Fact]
    public async Task ModelValidationUsesStableProblemDetailsContract()
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);
        await ApiTestClientSecurity.EnableAntiforgeryAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("urn:algojudge:error:validation",
            problem.GetProperty("type").GetString());
        Assert.Equal("validation", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.True(problem.GetProperty("errors").TryGetProperty("UserName", out _));
    }

    [Theory]
    [InlineData("/api/problems?pageNumber=0")]
    [InlineData("/api/problems?pageSize=101")]
    [InlineData("/api/problems?difficulty=999")]
    public async Task InvalidPaginationReturnsBadRequest(string path)
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthenticationChallengeUsesStableProblemDetailsContract()
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/api/submissions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authentication", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task InternalAuthoringEndpointRequiresAuthenticationAndStaysOutOfPublicOpenApi()
    {
        await using var factory = new AlgoJudgeApiFactory(UnusedConnectionString);
        using var client = CreateClient(factory);

        var response = await client.GetAsync($"/api/internal/admin/problem-drafts/{Guid.NewGuid()}");
        var openApi = await client.GetStringAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("/api/internal/admin", openApi, StringComparison.Ordinal);
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

    private static void AssertSecurityRequired(
        JsonElement paths,
        string path,
        string method,
        params string[] schemes)
    {
        var requirements = paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("security")
            .EnumerateArray()
            .ToArray();
        var requirement = Assert.Single(requirements);
        Assert.Equal(schemes.Length, requirement.EnumerateObject().Count());
        Assert.All(schemes, scheme =>
            Assert.True(requirement.TryGetProperty(scheme, out _)));
    }

    private static void AssertOptionalCookie(
        JsonElement paths,
        string path,
        string method)
    {
        var requirements = paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("security")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(2, requirements.Length);
        Assert.Contains(requirements, requirement =>
            !requirement.EnumerateObject().Any());
        Assert.Contains(requirements, requirement =>
            requirement.TryGetProperty("CookieSession", out _));
    }
}
