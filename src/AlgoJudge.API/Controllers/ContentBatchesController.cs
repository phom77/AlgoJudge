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
[Route("api/internal/admin/content-batches")]
public sealed class ContentBatchesController : ControllerBase
{
    private readonly IContentBatchService _service;

    public ContentBatchesController(IContentBatchService service) => _service = service;

    [HttpPost]
    [RequestSizeLimit(128L * 1024 * 1024)]
    public async Task<ActionResult<ContentBatchResponse>> Create(
        CreateContentBatchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.CreateAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { batchId = response.Id }, response);
    }

    [HttpGet]
    public Task<PagedResponse<ContentBatchListItemResponse>> GetAll(
        [FromQuery] ContentBatchListQuery query,
        CancellationToken cancellationToken) =>
        _service.GetBatchesAsync(query, cancellationToken);

    [HttpGet("{batchId:guid}")]
    public Task<ContentBatchResponse> Get(
        Guid batchId,
        CancellationToken cancellationToken) =>
        _service.GetAsync(batchId, cancellationToken);

    [HttpPost("{batchId:guid}/start")]
    public async Task<ActionResult<ContentBatchResponse>> Start(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var response = await _service.StartAsync(UserId(), batchId, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { batchId }, response);
    }

    [HttpPost("{batchId:guid}/resume")]
    public async Task<ActionResult<ContentBatchResponse>> Resume(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var response = await _service.ResumeAsync(UserId(), batchId, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { batchId }, response);
    }

    [HttpPost("{batchId:guid}/retry")]
    public async Task<ActionResult<ContentBatchResponse>> Retry(
        Guid batchId,
        RetryContentBatchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _service.RetryAsync(
            UserId(),
            batchId,
            request,
            cancellationToken);
        return AcceptedAtAction(nameof(Get), new { batchId }, response);
    }

    [HttpPost("{batchId:guid}/publish")]
    public Task<ContentBatchResponse> Publish(
        Guid batchId,
        PublishContentBatchRequest request,
        CancellationToken cancellationToken) =>
        _service.PublishAsync(UserId(), batchId, request, cancellationToken);

    private Guid UserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
