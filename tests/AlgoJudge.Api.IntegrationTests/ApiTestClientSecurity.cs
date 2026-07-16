using AlgoJudge.API.Security;

namespace AlgoJudge.Api.IntegrationTests;

internal static class ApiTestClientSecurity
{
    public static async Task EnableAntiforgeryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        var cookieHeader = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(
                AuthCookieManager.AntiforgeryRequestCookieName + "=",
                StringComparison.Ordinal));
        var encodedToken = cookieHeader.Split(';', 2)[0].Split('=', 2)[1];

        client.DefaultRequestHeaders.Remove(AuthCookieManager.AntiforgeryHeaderName);
        client.DefaultRequestHeaders.Add(
            AuthCookieManager.AntiforgeryHeaderName,
            Uri.UnescapeDataString(encodedToken));
    }
}
