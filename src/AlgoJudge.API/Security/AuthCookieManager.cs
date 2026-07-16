using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Models.Auth;

namespace AlgoJudge.API.Security;

public sealed class AuthCookieManager
{
    public const string AccessCookieName = "__Host-algojudge-access";
    public const string RefreshCookieName = "__Secure-algojudge-refresh";
    public const string AntiforgeryCookieName = "__Host-algojudge-antiforgery";
    public const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public void WriteSession(HttpResponse response, AuthSessionResult session)
    {
        response.Cookies.Append(
            AccessCookieName,
            session.AccessToken,
            CreateSensitiveCookieOptions("/", session.AccessTokenExpiresAt));
        response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            CreateSensitiveCookieOptions("/api/auth", session.RefreshTokenExpiresAt));
    }

    public void WriteAntiforgeryRequestToken(HttpResponse response, string token)
    {
        response.Cookies.Append(
            AntiforgeryRequestCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });
    }

    public string? ReadRefreshToken(HttpRequest request)
    {
        return request.Cookies[RefreshCookieName];
    }

    public void DeleteSession(HttpResponse response)
    {
        response.Cookies.Delete(AccessCookieName, CreateDeletionOptions("/"));
        response.Cookies.Delete(RefreshCookieName, CreateDeletionOptions("/api/auth"));
    }

    public static AuthResponse ToResponse(AuthSessionResult session)
    {
        return new AuthResponse
        {
            UserName = session.UserName,
            Email = session.Email,
            ExpiresAt = session.AccessTokenExpiresAt
        };
    }

    private static CookieOptions CreateSensitiveCookieOptions(
        string path,
        DateTime expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = path,
            Expires = new DateTimeOffset(expiresAt),
            IsEssential = true
        };
    }

    private static CookieOptions CreateDeletionOptions(string path)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = path,
            IsEssential = true
        };
    }
}
