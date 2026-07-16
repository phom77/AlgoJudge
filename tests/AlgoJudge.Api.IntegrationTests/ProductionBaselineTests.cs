using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlgoJudge.Api.IntegrationTests;

[Collection(ApiIntegrationCollection.Name)]
public class ProductionBaselineTests
{
    [PostgreSqlFact]
    public async Task ApiUsesPostgreSqlAndExposesStableOperationalContracts()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        var liveness = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);

        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        var readinessDocument = await readiness.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", readinessDocument.GetProperty("status").GetString());
        Assert.Contains(
            readinessDocument.GetProperty("checks").EnumerateArray(),
            check => check.GetProperty("name").GetString() == "postgresql" &&
                     check.GetProperty("status").GetString() == "Healthy");

        var catalogue = await client.GetAsync("/api/problems");
        Assert.Equal(HttpStatusCode.OK, catalogue.StatusCode);

        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("AlgoJudge API", openApi.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", openApi.GetProperty("info").GetProperty("version").GetString());
        var cataloguePath = openApi.GetProperty("paths").GetProperty("/api/problems");
        var successResponse = cataloguePath
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200");
        Assert.True(
            successResponse
                .GetProperty("content")
                .GetProperty("application/json")
                .TryGetProperty("schema", out _));
    }

    [PostgreSqlFact]
    public async Task ApplicationErrorsAndAuthenticationFailuresUseProblemDetails()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        var invalidFilter = await client.GetAsync("/api/problems?solved=true");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
        Assert.Equal("application/problem+json", invalidFilter.Content.Headers.ContentType?.MediaType);
        var validationProblem = await invalidFilter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, validationProblem.GetProperty("status").GetInt32());
        Assert.Equal("validation", validationProblem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            validationProblem.GetProperty("traceId").GetString()));

        var invalidRequest = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRequest.StatusCode);
        Assert.Equal("application/problem+json", invalidRequest.Content.Headers.ContentType?.MediaType);
        var modelValidationProblem = await invalidRequest.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("urn:algojudge:error:validation",
            modelValidationProblem.GetProperty("type").GetString());
        Assert.Equal("validation", modelValidationProblem.GetProperty("code").GetString());
        Assert.True(modelValidationProblem.GetProperty("errors").TryGetProperty("UserName", out _));

        var invalidPage = await client.GetAsync("/api/problems?pageNumber=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        var authenticationFailure = await client.GetAsync("/api/submissions");
        Assert.Equal(HttpStatusCode.Unauthorized, authenticationFailure.StatusCode);
        Assert.Equal(
            "application/problem+json",
            authenticationFailure.Content.Headers.ContentType?.MediaType);
        var authenticationProblem = await authenticationFailure.Content
            .ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authentication", authenticationProblem.GetProperty("code").GetString());
    }

    [PostgreSqlFact]
    public async Task GlobalRateLimitReturnsProblemDetails()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString, permitLimit: 2);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/problems")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/problems")).StatusCode);

        var rejected = await client.GetAsync("/api/problems");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    private static HttpClient CreateClient(AlgoJudgeApiFactory factory)
    {
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }
}
