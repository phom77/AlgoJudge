using AlgoJudge.API.Security;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlgoJudge.Api.IntegrationTests;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AuthCookieSecurityTests
{
    [Fact]
    public async Task CsrfBootstrapIssuesProtectedAndReadableCookies()
    {
        const string unusedConnection =
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";
        await using var factory = new AlgoJudgeApiFactory(unusedConnection);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(
            cookies,
            value => value.StartsWith(
                AuthCookieManager.AntiforgeryCookieName + "=",
                StringComparison.Ordinal));
        var requestTokenCookie = Assert.Single(
            cookies,
            value => value.StartsWith(
                AuthCookieManager.AntiforgeryRequestCookieName + "=",
                StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", requestTokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", requestTokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", requestTokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/", requestTokenCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevelopmentCsrfBootstrapSupportsTheHttpAngularProxy()
    {
        const string unusedConnection =
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";
        await using var factory = new AlgoJudgeApiFactory(
            unusedConnection,
            environment: "Development");
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        var protectedCookie = Assert.Single(
            cookies,
            value => value.StartsWith(
                AuthCookieManager.DevelopmentAntiforgeryCookieName + "=",
                StringComparison.Ordinal));
        var requestTokenCookie = Assert.Single(
            cookies,
            value => value.StartsWith(
                AuthCookieManager.AntiforgeryRequestCookieName + "=",
                StringComparison.Ordinal));
        Assert.DoesNotContain("secure", protectedCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", requestTokenCookie, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task AuthenticationUsesHttpOnlyCookiesAndNeverReturnsTokens()
    {
        await using var database = await ApiPostgreSqlDatabase.CreateAsync();
        await using var factory = new AlgoJudgeApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        await ApiTestClientSecurity.EnableAntiforgeryAsync(client);

        var unique = Guid.NewGuid().ToString("N");
        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName = $"cookie_{unique}",
                email = $"cookie_{unique}@example.test",
                password = "test-password-123",
                fullName = "Cookie Test User"
            });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.False(body.TryGetProperty("refreshToken", out _));
        Assert.False(body.TryGetProperty("tokenType", out _));

        var cookies = register.Headers.GetValues("Set-Cookie").ToArray();
        AssertSensitiveCookie(
            cookies,
            AuthCookieManager.AccessCookieName,
            "Path=/");
        AssertSensitiveCookie(
            cookies,
            AuthCookieManager.RefreshCookieName,
            "Path=/api/auth");

        var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        var refresh = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        AssertSensitiveCookie(
            refresh.Headers.GetValues("Set-Cookie").ToArray(),
            AuthCookieManager.RefreshCookieName,
            "Path=/api/auth");

        var revoke = await client.PostAsync("/api/auth/revoke", content: null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Contains(
            revoke.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(
                AuthCookieManager.AccessCookieName + "=",
                StringComparison.Ordinal));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task UnsafeCookieRequestWithoutAntiforgeryTokenIsRejected()
    {
        const string unusedConnection =
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";
        await using var factory = new AlgoJudgeApiFactory(unusedConnection);
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "not_reached",
            password = "not-reached"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("csrf", problem.GetProperty("code").GetString());
    }

    private static void AssertSensitiveCookie(
        IEnumerable<string> cookies,
        string name,
        string expectedPath)
    {
        var cookie = Assert.Single(cookies, value => value.StartsWith(
            name + "=",
            StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedPath, cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(AlgoJudgeApiFactory factory)
    {
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }
}
