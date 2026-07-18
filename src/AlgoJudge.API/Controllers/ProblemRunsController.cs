using AlgoJudge.API.ErrorHandling;
using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers;

[Route("api/problems/{slug}/runs")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProblemRunsController : ControllerBase
{
    private readonly IRunService _runs;
    public ProblemRunsController(IRunService runs) => _runs = runs;

    [HttpPost]
    [ProducesResponseType(typeof(RunResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RunResponse>> Create(string slug, [FromBody] CreateRunRequest request)
    {
        var result = await _runs.CreateAsync(slug, request, GetUserId(), HttpContext.RequestAborted);
        return Created($"/api/runs/{result.Id}", result);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
            throw new AuthenticationException("Token is invalid.");
        return id;
    }
}
