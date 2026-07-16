using AlgoJudge.API.ErrorHandling;
using AlgoJudge.API.Security;
using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers;

[Route("api/auth")]
[ApiController]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AuthCookieManager _cookies;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        IAuthService authService,
        AuthCookieManager cookies,
        IAntiforgery antiforgery)
    {
        _authService = authService;
        _cookies = cookies;
        _antiforgery = antiforgery;
    }

    [HttpGet("csrf")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        _cookies.WriteAntiforgeryRequestToken(Response, tokens.RequestToken!);
        return NoContent();
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request)
    {
        var session = await _authService.RegisterAsync(request);
        _cookies.WriteSession(Response, session);
        return Ok(AuthCookieManager.ToResponse(session));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request)
    {
        var session = await _authService.LoginAsync(request);
        _cookies.WriteSession(Response, session);
        return Ok(AuthCookieManager.ToResponse(session));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var refreshToken = _cookies.ReadRefreshToken(Request)
            ?? throw new AuthenticationException("Refresh cookie is missing.");
        var session = await _authService.RefreshAsync(refreshToken);
        _cookies.WriteSession(Response, session);
        return Ok(AuthCookieManager.ToResponse(session));
    }

    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke()
    {
        var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (callerIdClaim == null ||
            !Guid.TryParse(callerIdClaim.Value, out var callerId))
        {
            throw new AuthenticationException("Token is invalid.");
        }

        var refreshToken = _cookies.ReadRefreshToken(Request)
            ?? throw new AuthenticationException("Refresh cookie is missing.");
        await _authService.RevokeAsync(refreshToken, callerId);
        _cookies.DeleteSession(Response);
        return NoContent();
    }

    [HttpGet("session")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthResponse> GetSession()
    {
        var userName = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? throw new AuthenticationException("Token is invalid.");
        var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? throw new AuthenticationException("Token is invalid.");
        var expires = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (!long.TryParse(expires, out var expiresUnixSeconds))
            throw new AuthenticationException("Token is invalid.");

        return Ok(new AuthResponse
        {
            UserName = userName,
            Email = email,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnixSeconds).UtcDateTime
        });
    }
}
