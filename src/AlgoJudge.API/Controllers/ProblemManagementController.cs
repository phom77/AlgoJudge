using System.Security.Claims;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "admin-v1")]
[Authorize(Policy = "Admin")]
[Route("api/internal/admin/problems")]
public sealed class ProblemManagementController : ControllerBase
{
    private readonly IProblemManagementService managementService;
    private readonly IProblemAuthoringService authoringService;

    public ProblemManagementController(
        IProblemManagementService managementService,
        IProblemAuthoringService authoringService)
    {
        this.managementService = managementService;
        this.authoringService = authoringService;
    }

    [HttpGet]
    public Task<PagedResponse<AdminProblemListItemResponse>> GetAll(
        [FromQuery] AdminProblemListQuery query,
        CancellationToken cancellationToken) =>
        managementService.GetProblemsAsync(query, cancellationToken);

    [HttpGet("{problemId:int}")]
    public Task<AdminProblemResponse> Get(int problemId, CancellationToken cancellationToken) =>
        managementService.GetProblemAsync(problemId, cancellationToken);

    [HttpPost("{problemId:int}/revisions")]
    public async Task<ActionResult<ProblemDraftResponse>> CreateRevision(
        int problemId,
        CancellationToken cancellationToken)
    {
        var response = await authoringService.CreateManagedNextRevisionAsync(
            UserId(), problemId, cancellationToken);
        return Created($"/api/internal/admin/problem-drafts/{response.RevisionId}", response);
    }

    [HttpPost("{problemId:int}/archive")]
    public async Task<IActionResult> Archive(int problemId, CancellationToken cancellationToken)
    {
        await managementService.ArchiveAsync(problemId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{problemId:int}/restore")]
    public async Task<IActionResult> Restore(int problemId, CancellationToken cancellationToken)
    {
        await managementService.RestoreAsync(problemId, cancellationToken);
        return NoContent();
    }

    private Guid UserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
