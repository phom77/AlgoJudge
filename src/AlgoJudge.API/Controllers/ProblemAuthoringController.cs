using System.Security.Claims;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = "Maintainer")]
[Route("api/internal/admin/problem-drafts")]
public sealed class ProblemAuthoringController : ControllerBase
{
    private readonly IProblemAuthoringService _service;
    public ProblemAuthoringController(IProblemAuthoringService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<ProblemDraftResponse>> Create(
        CreateProblemDraftRequest request, CancellationToken cancellationToken)
    {
        var response = await _service.CreateDraftAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { revisionId = response.RevisionId }, response);
    }

    [HttpPost("problems/{problemId:int}/revisions")]
    public async Task<ActionResult<ProblemDraftResponse>> CreateNextRevision(int problemId, CancellationToken cancellationToken)
    {
        var response = await _service.CreateNextRevisionAsync(UserId(), problemId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { revisionId = response.RevisionId }, response);
    }

    [HttpGet("{revisionId:guid}")]
    public Task<ProblemDraftResponse> Get(Guid revisionId, CancellationToken cancellationToken) =>
        _service.GetDraftAsync(UserId(), revisionId, cancellationToken);

    [HttpPut("{revisionId:guid}/metadata")]
    public Task<ProblemDraftResponse> UpdateMetadata(Guid revisionId, UpdateProblemDraftRequest request, CancellationToken cancellationToken) =>
        _service.UpdateMetadataAsync(UserId(), revisionId, request, cancellationToken);

    [HttpPut("{revisionId:guid}/signature")]
    public Task<ProblemDraftResponse> UpdateSignature(Guid revisionId, UpdateFunctionSignatureRequest request, CancellationToken cancellationToken) =>
        _service.UpdateSignatureAsync(UserId(), revisionId, request, cancellationToken);

    [HttpPut("{revisionId:guid}/handwritten-cases")]
    public Task<ProblemDraftResponse> UpdateHandwrittenCases(Guid revisionId, UpdateHandwrittenCasesRequest request, CancellationToken cancellationToken) =>
        _service.UpdateHandwrittenCasesAsync(UserId(), revisionId, request, cancellationToken);

    [HttpPut("{revisionId:guid}/sources")]
    public Task<ProblemDraftResponse> UpdateSources(Guid revisionId, UpdateAuthoringSourcesRequest request, CancellationToken cancellationToken) =>
        _service.UpdateSourcesAsync(UserId(), revisionId, request, cancellationToken);

    [HttpPost("{revisionId:guid}/generation")]
    public async Task<ActionResult<ContentGenerationStatusResponse>> Generate(Guid revisionId, CancellationToken cancellationToken)
    {
        var response = await _service.StartGenerationAsync(UserId(), revisionId, cancellationToken);
        return AcceptedAtAction(nameof(GetGenerationStatus), new { revisionId }, response);
    }

    [HttpGet("{revisionId:guid}/generation")]
    public Task<ContentGenerationStatusResponse> GetGenerationStatus(Guid revisionId, CancellationToken cancellationToken) =>
        _service.GetGenerationStatusAsync(UserId(), revisionId, cancellationToken);

    [HttpGet("{revisionId:guid}/suite-review")]
    public Task<GeneratedSuiteReviewResponse> GetSuiteReview(Guid revisionId, CancellationToken cancellationToken) =>
        _service.GetSuiteReviewAsync(UserId(), revisionId, cancellationToken);

    [HttpPost("{revisionId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid revisionId, CancellationToken cancellationToken)
    {
        await _service.PublishAsync(UserId(), revisionId, cancellationToken);
        return NoContent();
    }

    private Guid UserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
