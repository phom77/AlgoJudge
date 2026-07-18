using AlgoJudge.API.ErrorHandling;
using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers;

[Route("api/runs")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class RunsController : ControllerBase
{
    private readonly IRunService _runs;
    public RunsController(IRunService runs) => _runs = runs;

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunResponse>> Get(Guid id)
    {
        var result = await _runs.GetByIdAsync(id, GetUserId(), HttpContext.RequestAborted);
        return result is null
            ? throw new ResourceNotFoundException($"Run '{id}' was not found.")
            : Ok(result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new AuthenticationException("Token is invalid.");
        return id;
    }
}
