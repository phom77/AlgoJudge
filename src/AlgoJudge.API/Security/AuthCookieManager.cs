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
    public const string DevelopmentAccessCookieName = "algojudge-access";
    public const string DevelopmentRefreshCookieName = "algojudge-refresh";
    public const string DevelopmentAntiforgeryCookieName = "algojudge-antiforgery";

    private readonly bool _useSecureCookies;

    public AuthCookieManager(IHostEnvironment environment)
    {
        _useSecureCookies = !environment.IsDevelopment();
    }

    public void WriteSession(HttpResponse response, AuthSessionResult session)
    {
        response.Cookies.Append(
            GetAccessCookieName(),
            session.AccessToken,
            CreateSensitiveCookieOptions("/", session.AccessTokenExpiresAt));
        response.Cookies.Append(
            GetRefreshCookieName(),
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
                Secure = _useSecureCookies,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });
    }

    public string? ReadAccessToken(HttpRequest request)
    {
        return request.Cookies[GetAccessCookieName()];
    }

    public string? ReadRefreshToken(HttpRequest request)
    {
        return request.Cookies[GetRefreshCookieName()];
    }

    public void DeleteSession(HttpResponse response)
    {
        response.Cookies.Delete(GetAccessCookieName(), CreateDeletionOptions("/"));
        response.Cookies.Delete(GetRefreshCookieName(), CreateDeletionOptions("/api/auth"));
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

    private CookieOptions CreateSensitiveCookieOptions(
        string path,
        DateTime expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _useSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = path,
            Expires = new DateTimeOffset(expiresAt),
            IsEssential = true
        };
    }

    private CookieOptions CreateDeletionOptions(string path)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = _useSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = path,
            IsEssential = true
        };
    }

    private string GetAccessCookieName()
    {
        return _useSecureCookies ? AccessCookieName : DevelopmentAccessCookieName;
    }

    private string GetRefreshCookieName()
    {
        return _useSecureCookies ? RefreshCookieName : DevelopmentRefreshCookieName;
    }
}
