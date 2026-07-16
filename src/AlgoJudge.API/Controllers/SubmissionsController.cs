using AlgoJudge.API.ErrorHandling;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers;

[Route("api/submissions")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubmissionResponse>> SubmitCode(
        [FromBody] CreateSubmissionRequest request)
    {
        var userId = GetUserIdFromToken();
        var result = await _submissionService.SubmitCodeAsync(request, userId);
        return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionResponse>> GetSubmissionById(Guid id)
    {
        var result = await _submissionService.GetSubmissionByIdAsync(
            id,
            GetUserIdFromToken());
        if (result == null)
            throw new ResourceNotFoundException($"Submission '{id}' was not found.");

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResponse<SubmissionResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<SubmissionResponse>>> GetHistory(
        [FromQuery] SubmissionHistoryQuery query)
    {
        var result = await _submissionService.GetHistoryAsync(
            GetUserIdFromToken(),
            query);
        return Ok(result);
    }

    private Guid GetUserIdFromToken()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (claim == null || !Guid.TryParse(claim.Value, out var id))
            throw new AuthenticationException("Token is invalid.");

        return id;
    }
}
