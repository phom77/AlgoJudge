using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Services;

public sealed partial class ContentBatchService : IContentBatchService
{
    private const int MaximumBatchItems = 1_000;
    private const int MaximumSourceBytes = 1024 * 1024;
    private const long MaximumBatchSourceBytes = 100L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly IContentBatchRepository _repository;
    private readonly IProblemAuthoringRepository _authoringRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ContentBatchService(
        IContentBatchRepository repository,
        IProblemAuthoringRepository authoringRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _authoringRepository = authoringRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ContentBatchResponse> CreateAsync(
        Guid adminUserId,
        CreateContentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmin(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CatalogName) ||
            request.CatalogName.Length > 255 ||
            request.Items is null ||
            request.Items.Count is < 1 or > MaximumBatchItems)
        {
            throw new RequestValidationException(
                $"A batch requires a catalog name and 1-{MaximumBatchItems} items.");
        }
        if (request.Items.Sum(SourceBytes) > MaximumBatchSourceBytes)
        {
            throw new RequestValidationException(
                "The batch exceeds the 100 MiB private-source limit.");
        }

        var now = DateTime.UtcNow;
        var batch = new ContentBatch
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = adminUserId,
            CatalogName = request.CatalogName.Trim(),
            Status = ContentBatchStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };
        for (var index = 0; index < request.Items.Count; index++)
            batch.Items.Add(CreateItem(batch, request.Items[index], index + 1, now));

        foreach (var duplicate in batch.Items
                     .Where(item =>
                         item.Slug.Length <= 160 &&
                         SlugPattern().IsMatch(item.Slug))
                     .GroupBy(item => item.Slug, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach (var item in duplicate)
            {
                if (item.Status != ContentBatchItemStatus.Failed)
                {
                    FailItem(
                        item,
                        "duplicate_slug",
                        "The catalog contains a duplicate problem slug.",
                        now);
                }
            }
        }

        Audit(batch, adminUserId, "batch.create", "succeeded");
        await _repository.AddAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(batch, includeAudit: true);
    }

    public async Task<PagedResponse<ContentBatchListItemResponse>> GetBatchesAsync(
        ContentBatchListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageNumber < 1 ||
            query.PageSize is < 1 or > 100 ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw new RequestValidationException("Content batch query is invalid.");
        }

        var page = await _repository.GetPagedAsync(
            query.Status,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        return new PagedResponse<ContentBatchListItemResponse>
        {
            Items = page.Items.Select(MapListItem).ToArray(),
            TotalCount = page.TotalCount,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize
        };
    }

    public async Task<ContentBatchResponse> GetAsync(
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        Map(await GetRequiredAsync(batchId, includeAudit: true, cancellationToken), includeAudit: true);

    public Task<ContentBatchResponse> StartAsync(
        Guid adminUserId,
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        StartOrResumeAsync(adminUserId, batchId, resume: false, cancellationToken);

    public Task<ContentBatchResponse> ResumeAsync(
        Guid adminUserId,
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        StartOrResumeAsync(adminUserId, batchId, resume: true, cancellationToken);

    public async Task<ContentBatchResponse> RetryAsync(
        Guid adminUserId,
        Guid batchId,
        RetryContentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmin(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ItemIds is null ||
            request.ItemIds.Count is < 1 or > MaximumBatchItems ||
            request.ItemIds.Distinct().Count() != request.ItemIds.Count)
        {
            throw new RequestValidationException(
                "Retry requires a unique non-empty item ID list.");
        }

        var batch = await GetRequiredAsync(batchId, includeAudit: true, cancellationToken);
        if (batch.Status is ContentBatchStatus.Publishing or ContentBatchStatus.Completed)
            throw new ConflictException($"Batch cannot retry from {batch.Status}.");
        var requested = batch.Items.Where(item => request.ItemIds.Contains(item.Id)).ToArray();
        if (requested.Length != request.ItemIds.Count)
            throw new RequestValidationException("One or more retry items do not belong to the batch.");
        if (requested.Any(item => item.Status != ContentBatchItemStatus.Failed))
            throw new ConflictException("Only failed batch items can be retried.");

        foreach (var item in requested)
        {
            if (item.Revision is { Status: AuthoringRevisionStatus.Ready })
            {
                item.Status = ContentBatchItemStatus.Ready;
                item.SafeFailureCategory = null;
                item.SafeFailureMessage = null;
                item.FinishedAt = null;
                item.UpdatedAt = DateTime.UtcNow;
                Audit(batch, adminUserId, "item.retry", "ready", item);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (item.Revision is { Status: AuthoringRevisionStatus.Draft } revision)
            {
                await EnqueueAsync(batch, item, revision, retry: true, cancellationToken);
                Audit(batch, adminUserId, "item.retry", "enqueued", item);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (item.RevisionId is null &&
                item.SafeFailureCategory is not ("invalid_definition" or "invalid_path" or "duplicate_slug"))
            {
                item.Status = ContentBatchItemStatus.Pending;
                item.SafeFailureCategory = null;
                item.SafeFailureMessage = null;
                item.FinishedAt = null;
                await PrepareItemAsync(batch, item, adminUserId, retry: true, cancellationToken);
                continue;
            }

            Audit(batch, adminUserId, "item.retry", "rejected", item, "not_retryable");
        }

        RefreshBatchStatus(batch);
        Touch(batch);
        Audit(batch, adminUserId, "batch.retry", "completed");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(batch, includeAudit: true);
    }

    public async Task<ContentBatchResponse> PublishAsync(
        Guid adminUserId,
        Guid batchId,
        PublishContentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAdmin(adminUserId);
        ArgumentNullException.ThrowIfNull(request);
        if (request.RevisionIds is null ||
            request.RevisionIds.Count == 0 ||
            request.RevisionIds.Distinct().Count() != request.RevisionIds.Count)
        {
            throw new RequestValidationException(
                "Publish requires a unique non-empty approved revision ID list.");
        }

        var batch = await GetRequiredAsync(batchId, includeAudit: true, cancellationToken);
        if (batch.Status != ContentBatchStatus.ReadyForReview)
            throw new ConflictException($"Batch cannot publish from {batch.Status}.");
        var items = batch.Items
            .Where(item => item.RevisionId.HasValue &&
                           request.RevisionIds.Contains(item.RevisionId.Value))
            .ToArray();
        if (items.Length != request.RevisionIds.Count)
            throw new RequestValidationException(
                "Every approved revision must belong to this batch.");
        if (items.Any(item =>
                item.Status != ContentBatchItemStatus.Ready ||
                item.Revision?.Status != AuthoringRevisionStatus.Ready))
        {
            throw new ConflictException("Only Ready batch revisions can be published.");
        }

        batch.Status = ContentBatchStatus.Publishing;
        Touch(batch);
        Audit(batch, adminUserId, "batch.publish", "started");
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var item in items.OrderBy(item => item.Ordinal))
        {
            var published = await _authoringRepository.PublishAsync(
                item.RevisionId!.Value,
                item.Revision!.OwnerUserId,
                cancellationToken);
            if (published)
            {
                item.Status = ContentBatchItemStatus.Published;
                item.SafeFailureCategory = null;
                item.SafeFailureMessage = null;
                item.FinishedAt = DateTime.UtcNow;
                Audit(batch, adminUserId, "item.publish", "succeeded", item);
            }
            else
            {
                FailItem(
                    item,
                    "publish_conflict",
                    "The Ready revision could not be published.",
                    DateTime.UtcNow);
                Audit(batch, adminUserId, "item.publish", "failed", item, "publish_conflict");
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        RefreshBatchStatus(batch);
        Touch(batch);
        Audit(batch, adminUserId, "batch.publish", "completed");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(batch, includeAudit: true);
    }

    private async Task<ContentBatchResponse> StartOrResumeAsync(
        Guid adminUserId,
        Guid batchId,
        bool resume,
        CancellationToken cancellationToken)
    {
        ValidateAdmin(adminUserId);
        var batch = await GetRequiredAsync(batchId, includeAudit: true, cancellationToken);
        var allowed = resume
            ? batch.Status is ContentBatchStatus.Created or
                ContentBatchStatus.Validating or
                ContentBatchStatus.Generating or
                ContentBatchStatus.ReadyForReview
            : batch.Status == ContentBatchStatus.Created;
        if (!allowed)
            throw new ConflictException($"Batch cannot {(resume ? "resume" : "start")} from {batch.Status}.");

        batch.Status = ContentBatchStatus.Validating;
        batch.StartedAt ??= DateTime.UtcNow;
        Touch(batch);
        Audit(batch, adminUserId, resume ? "batch.resume" : "batch.start", "started");
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var item in batch.Items
                     .Where(item => item.Status == ContentBatchItemStatus.Pending)
                     .OrderBy(item => item.Ordinal))
        {
            await PrepareItemAsync(batch, item, adminUserId, retry: false, cancellationToken);
        }

        RefreshBatchStatus(batch);
        Touch(batch);
        Audit(batch, adminUserId, resume ? "batch.resume" : "batch.start", "completed");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(batch, includeAudit: true);
    }

    private async Task PrepareItemAsync(
        ContentBatch batch,
        ContentBatchItem item,
        Guid adminUserId,
        bool retry,
        CancellationToken cancellationToken)
    {
        try
        {
            var definition = DeserializeDefinition(item.DefinitionJson);
            var problem = await _repository.GetProblemBySlugAsync(item.Slug, cancellationToken);
            var latest = problem?.AuthoringRevisions
                .OrderByDescending(revision => revision.RevisionNumber)
                .FirstOrDefault();
            if (item.ContentHash is not null &&
                (latest?.Status is AuthoringRevisionStatus.Ready or
                    AuthoringRevisionStatus.Published) &&
                string.Equals(latest?.ContentHash, item.ContentHash, StringComparison.Ordinal))
            {
                item.Problem = problem;
                item.ProblemId = problem!.Id;
                item.Revision = latest;
                item.RevisionId = latest!.Id;
                item.Status = ContentBatchItemStatus.Skipped;
                item.FinishedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
                Audit(batch, adminUserId, "item.import", "skipped", item);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            ProblemAuthoringRevision revision;
            switch (item.Action)
            {
                case ContentBatchImportAction.Create when problem is null:
                    problem = CreateProblem(item, definition);
                    revision = CreateRevision(item, problem, batch.CreatedByUserId, 1);
                    await _repository.AddProblemAsync(problem, cancellationToken);
                    await _repository.AddRevisionAsync(revision, cancellationToken);
                    break;
                case ContentBatchImportAction.Create:
                    throw new BatchItemFailureException(
                        "problem_exists",
                        "Create requires a slug that does not exist.");
                case ContentBatchImportAction.UpdateDraft
                    when problem is not null && latest?.Status == AuthoringRevisionStatus.Draft:
                    revision = latest;
                    ApplySnapshot(item, revision);
                    break;
                case ContentBatchImportAction.UpdateDraft:
                    throw new BatchItemFailureException(
                        "draft_not_editable",
                        "update-draft requires an existing Draft revision.");
                case ContentBatchImportAction.NewRevision
                    when problem is not null &&
                         (latest is null && problem.Status == ProblemStatus.Published ||
                          latest?.Status == AuthoringRevisionStatus.Published):
                    revision = CreateRevision(
                        item,
                        problem,
                        batch.CreatedByUserId,
                        (latest?.RevisionNumber ?? 0) + 1);
                    await _repository.AddRevisionAsync(revision, cancellationToken);
                    break;
                case ContentBatchImportAction.NewRevision:
                    throw new BatchItemFailureException(
                        "new_revision_conflict",
                        "new-revision requires an existing Published problem without an editable revision.");
                case ContentBatchImportAction.Skip:
                    item.Status = ContentBatchItemStatus.Skipped;
                    item.FinishedAt = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                    Audit(batch, adminUserId, "item.import", "skipped", item);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    return;
                default:
                    throw new BatchItemFailureException(
                        "invalid_action",
                        "The catalog action is invalid.");
            }

            item.Problem = problem;
            item.Revision = revision;
            await EnqueueAsync(batch, item, revision, retry, cancellationToken);
            Audit(batch, adminUserId, "item.import", "enqueued", item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (BatchItemFailureException exception)
        {
            FailItem(item, exception.Category, exception.SafeMessage, DateTime.UtcNow);
            Audit(batch, adminUserId, "item.import", "failed", item, exception.Category);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnqueueAsync(
        ContentBatch batch,
        ContentBatchItem item,
        ProblemAuthoringRevision revision,
        bool retry,
        CancellationToken cancellationToken)
    {
        if (revision.GenerationJobs.Any(job =>
                job.Status is ContentGenerationJobStatus.Pending or ContentGenerationJobStatus.Running))
        {
            throw new BatchItemFailureException(
                "generation_already_active",
                "The revision already has an active generation job.");
        }

        revision.Status = AuthoringRevisionStatus.Generating;
        revision.CandidateSuiteSha256 = null;
        revision.CandidateToolchain = null;
        revision.CandidateStatisticsJson = null;
        revision.CandidateCaseCount = null;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.ConcurrencyToken = Guid.NewGuid();
        var job = new ContentGenerationJob
        {
            Id = Guid.NewGuid(),
            Revision = revision,
            RevisionId = revision.Id,
            BatchItem = item,
            BatchItemId = item.Id,
            Status = ContentGenerationJobStatus.Pending,
            DefinitionSnapshotJson = revision.DefinitionJson,
            DefinitionSha256 = revision.DefinitionSha256,
            TimeLimitMs = revision.TimeLimitMs,
            MemoryLimitKb = revision.MemoryLimitKb,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddGenerationJobAsync(job, cancellationToken);
        item.Revision = revision;
        item.RevisionId = revision.Id;
        item.Problem = revision.Problem;
        item.ProblemId = revision.ProblemId == 0 ? null : revision.ProblemId;
        item.Status = retry
            ? ContentBatchItemStatus.Retrying
            : ContentBatchItemStatus.Generating;
        item.SafeFailureCategory = null;
        item.SafeFailureMessage = null;
        item.FinishedAt = null;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static long SourceBytes(CreateContentBatchItemRequest? item)
    {
        if (item?.Definition is null)
            return 0;
        var definition = item.Definition;
        return Encoding.UTF8.GetByteCount(definition.Generator?.Source ?? string.Empty) +
               Encoding.UTF8.GetByteCount(
                   definition.InputValidator?.Source ?? string.Empty) +
               Encoding.UTF8.GetByteCount(
                   definition.ReferenceSolution?.Source ?? string.Empty) +
               (definition.WrongSolutions?.Sum(solution =>
                   (long)Encoding.UTF8.GetByteCount(solution?.Source ?? string.Empty)) ?? 0);
    }

    private static ContentBatchItem CreateItem(
        ContentBatch batch,
        CreateContentBatchItemRequest request,
        int ordinal,
        DateTime now)
    {
        request ??= new CreateContentBatchItemRequest();
        var item = new ContentBatchItem
        {
            Id = Guid.NewGuid(),
            Batch = batch,
            BatchId = batch.Id,
            Ordinal = ordinal,
            CatalogPath = Bound(request.CatalogPath, 512),
            Action = request.Action,
            Status = request.Action == ContentBatchImportAction.Skip
                ? ContentBatchItemStatus.Skipped
                : ContentBatchItemStatus.Pending,
            ContentHash = string.IsNullOrWhiteSpace(request.ContentHash)
                ? null
                : request.ContentHash,
            Slug = Bound(request.Slug, 160),
            Title = Bound(request.Title, 255),
            StatementMarkdown = request.StatementMarkdown ?? string.Empty,
            ConstraintsMarkdown = request.ConstraintsMarkdown ?? string.Empty,
            Difficulty = request.Difficulty,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitKb = request.MemoryLimitKb,
            TagsJson = JsonSerializer.Serialize(request.Tags ?? [], JsonOptions),
            SamplesJson = JsonSerializer.Serialize(request.Samples ?? [], JsonOptions),
            DefinitionJson = request.Definition is null
                ? "{}"
                : JsonSerializer.Serialize(request.Definition, JsonOptions),
            GeneratorParametersJson = request.GeneratorParameters.ValueKind == JsonValueKind.Object
                ? request.GeneratorParameters.GetRawText()
                : "{}",
            CreatedAt = now,
            UpdatedAt = now,
            FinishedAt = request.Action == ContentBatchImportAction.Skip ? now : null
        };

        var failure = ValidateItem(request);
        if (failure is not null)
            FailItem(item, failure.Value.Category, failure.Value.Message, now);
        return item;
    }

    private static (string Category, string Message)? ValidateItem(
        CreateContentBatchItemRequest request)
    {
        if (request.ValidationFailureCategory is not null)
        {
            return request.ValidationFailureCategory switch
            {
                "invalid_path" => (
                    "invalid_path",
                    "The workspace item contains an unsafe or invalid path."),
                _ => (
                    "invalid_definition",
                    "The workspace item definition is invalid.")
            };
        }

        var slug = request.Slug ?? string.Empty;
        if (!IsSafeRelativePath(request.CatalogPath))
        {
            return (
                "invalid_path",
                "The workspace item contains an unsafe or invalid path.");
        }
        if (request.Definition is null ||
            request.ContentHash is null ||
            !HashPattern().IsMatch(request.ContentHash) ||
            request.CatalogPath.Length > 512 ||
            !SlugPattern().IsMatch(slug) ||
            slug.Length > 160 ||
            string.IsNullOrWhiteSpace(request.Title) ||
            request.Title.Length > 255 ||
            string.IsNullOrWhiteSpace(request.StatementMarkdown) ||
            string.IsNullOrWhiteSpace(request.ConstraintsMarkdown) ||
            request.TimeLimitMs is < 100 or > 10_000 ||
            request.MemoryLimitKb is < 16_384 or > 1_048_576 ||
            !Enum.IsDefined(request.Difficulty) ||
            !Enum.IsDefined(request.Action))
        {
            return ("invalid_definition", "The workspace item metadata is invalid.");
        }
        if (request.Tags is null ||
            request.Tags.Count > 10 ||
            request.Tags.Any(tag => !SlugPattern().IsMatch(tag ?? string.Empty)) ||
            request.Tags.Distinct(StringComparer.Ordinal).Count() != request.Tags.Count)
        {
            return ("invalid_definition", "The workspace item tags are invalid.");
        }
        if (request.GeneratorParameters.ValueKind != JsonValueKind.Object)
            return ("invalid_definition", "Generator parameters must be a JSON object.");
        if (!ValidateDefinition(request.Definition, request.Samples))
            return ("invalid_definition", "The workspace authoring definition is invalid.");
        return null;
    }

    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Length <= 512 &&
        !Path.IsPathRooted(path) &&
        !path.StartsWith("/", StringComparison.Ordinal) &&
        !WindowsDrivePath().IsMatch(path) &&
        !path.Contains('\\') &&
        path.Split('/').All(segment =>
            segment.Length > 0 && segment is not ("." or ".."));

    private static bool ValidateDefinition(
        ProblemAuthoringDefinition definition,
        IReadOnlyList<ProblemSampleRequest> samples)
    {
        try
        {
            definition.QualityPolicy.Validate();
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (definition.SchemaVersion != 1 ||
            definition.ExecutionMode != ProblemExecutionMode.Function ||
            definition.FunctionSignature is null ||
            string.IsNullOrWhiteSpace(definition.FunctionSignature.ClassName) ||
            string.IsNullOrWhiteSpace(definition.FunctionSignature.MethodName) ||
            definition.FunctionSignature.Parameters is null ||
            definition.FunctionSignature.Parameters.Count > 16 ||
            definition.FunctionSignature.Parameters.Any(parameter =>
                parameter is null || string.IsNullOrWhiteSpace(parameter.Name)) ||
            definition.FunctionSignature.Parameters.Select(parameter => parameter.Name)
                .Distinct(StringComparer.Ordinal).Count() !=
                definition.FunctionSignature.Parameters.Count ||
            definition.HandwrittenCases is null ||
            definition.HandwrittenCases.Count == 0 ||
            definition.Generator is not { Language: "csharp", SdkVersion: 1 } ||
            definition.InputValidator is not { Language: "csharp", SdkVersion: 1 } ||
            definition.ReferenceSolution is not { Language: "cpp17" } ||
            definition.WrongSolutions is null)
        {
            return false;
        }

        var sources = new[]
        {
            definition.Generator.Source,
            definition.InputValidator.Source,
            definition.ReferenceSolution.Source
        }.Concat(definition.WrongSolutions.Select(solution => solution.Source));
        if (definition.WrongSolutions.Count > 50 ||
            sources.Any(source =>
                string.IsNullOrWhiteSpace(source) ||
                Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes) ||
            definition.WrongSolutions.Any(solution =>
                solution is null ||
                solution.Language != "cpp17" ||
                !SlugPattern().IsMatch(solution.Name)) ||
            definition.WrongSolutions.Select(solution => solution.Name)
                .Distinct(StringComparer.Ordinal).Count() !=
                definition.WrongSolutions.Count)
        {
            return false;
        }

        if (definition.HandwrittenCases.Any(item =>
                item is null ||
                !SlugPattern().IsMatch(item.Name) ||
                item.Group != "handwritten" ||
                !ArgumentsMatch(item.Arguments, definition.FunctionSignature)))
        {
            return false;
        }
        if (samples is null || samples.Count is < 1 or > 20)
            return false;
        return samples.All(sample => SampleMatches(sample, definition.FunctionSignature));
    }

    private static bool ArgumentsMatch(JsonElement arguments, FunctionSignature signature) =>
        arguments.ValueKind == JsonValueKind.Object &&
        arguments.EnumerateObject().Count() == signature.Parameters.Count &&
        signature.Parameters.All(parameter =>
            arguments.TryGetProperty(parameter.Name, out var value) &&
            FunctionValueJsonValidator.Matches(value, parameter.Type));

    private static bool SampleMatches(ProblemSampleRequest sample, FunctionSignature signature)
    {
        if (sample is null ||
            string.IsNullOrWhiteSpace(sample.Input) ||
            string.IsNullOrWhiteSpace(sample.ExpectedOutput))
        {
            return false;
        }
        try
        {
            using var input = JsonDocument.Parse(sample.Input);
            using var output = JsonDocument.Parse(sample.ExpectedOutput);
            return ArgumentsMatch(input.RootElement, signature) &&
                   FunctionValueJsonValidator.Matches(output.RootElement, signature.ReturnType);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Problem CreateProblem(
        ContentBatchItem item,
        ProblemAuthoringDefinition definition)
    {
        var now = DateTime.UtcNow;
        return new Problem
        {
            Slug = item.Slug,
            Title = item.Title,
            StatementMarkdown = item.StatementMarkdown,
            ConstraintsMarkdown = item.ConstraintsMarkdown,
            Difficulty = item.Difficulty,
            TimeLimitMs = item.TimeLimitMs,
            MemoryLimitKb = item.MemoryLimitKb,
            ExecutionMode = ProblemExecutionMode.Function,
            FunctionSignatureJson = FunctionSignatureJsonSerializer.Serialize(
                definition.FunctionSignature),
            Status = ProblemStatus.Draft,
            JudgeVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ProblemAuthoringRevision CreateRevision(
        ContentBatchItem item,
        Problem problem,
        Guid ownerUserId,
        int revisionNumber)
    {
        var revision = new ProblemAuthoringRevision
        {
            Id = Guid.NewGuid(),
            Problem = problem,
            ProblemId = problem.Id,
            OwnerUserId = ownerUserId,
            RevisionNumber = revisionNumber,
            CreatedAt = DateTime.UtcNow
        };
        ApplySnapshot(item, revision);
        return revision;
    }

    private static void ApplySnapshot(
        ContentBatchItem item,
        ProblemAuthoringRevision revision)
    {
        revision.Status = AuthoringRevisionStatus.Draft;
        revision.Slug = item.Slug;
        revision.Title = item.Title;
        revision.StatementMarkdown = item.StatementMarkdown;
        revision.ConstraintsMarkdown = item.ConstraintsMarkdown;
        revision.Difficulty = item.Difficulty;
        revision.TimeLimitMs = item.TimeLimitMs;
        revision.MemoryLimitKb = item.MemoryLimitKb;
        revision.SamplesJson = item.SamplesJson;
        revision.DefinitionJson = item.DefinitionJson;
        revision.DefinitionSha256 = Hash(item.DefinitionJson);
        revision.ContentHash = item.ContentHash;
        revision.TagsJson = item.TagsJson;
        revision.CandidateSuiteSha256 = null;
        revision.CandidateToolchain = null;
        revision.CandidateStatisticsJson = null;
        revision.CandidateCaseCount = null;
        revision.UpdatedAt = DateTime.UtcNow;
        revision.PublishedAt = null;
        revision.ConcurrencyToken = Guid.NewGuid();
    }

    private static ProblemAuthoringDefinition DeserializeDefinition(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProblemAuthoringDefinition>(json, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new BatchItemFailureException(
                "invalid_definition",
                "The stored authoring definition is invalid.");
        }
    }

    private async Task<ContentBatch> GetRequiredAsync(
        Guid batchId,
        bool includeAudit,
        CancellationToken cancellationToken)
    {
        if (batchId == Guid.Empty)
            throw new RequestValidationException("Batch ID is required.");
        return await _repository.GetAsync(batchId, includeAudit, cancellationToken)
            ?? throw new ResourceNotFoundException("Content batch was not found.");
    }

    private static void RefreshBatchStatus(ContentBatch batch)
    {
        if (batch.Items.Any(item => item.Status == ContentBatchItemStatus.Pending))
        {
            batch.Status = ContentBatchStatus.Validating;
            return;
        }
        if (batch.Items.Any(item =>
                item.Status is ContentBatchItemStatus.Generating or
                    ContentBatchItemStatus.Retrying))
        {
            batch.Status = ContentBatchStatus.Generating;
            return;
        }
        if (batch.Items.Any(item => item.Status == ContentBatchItemStatus.Ready))
        {
            batch.Status = ContentBatchStatus.ReadyForReview;
            return;
        }
        if (batch.Items.All(item =>
                item.Status is ContentBatchItemStatus.Published or
                    ContentBatchItemStatus.Failed or
                    ContentBatchItemStatus.Skipped))
        {
            if (batch.Items.Any(item => item.Status == ContentBatchItemStatus.Published))
            {
                batch.Status = ContentBatchStatus.Completed;
                batch.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                batch.Status = ContentBatchStatus.ReadyForReview;
            }
        }
    }

    private static ContentBatchResponse Map(ContentBatch batch, bool includeAudit) => new()
    {
        Id = batch.Id,
        CatalogName = batch.CatalogName,
        Status = batch.Status,
        CreatedByUserId = batch.CreatedByUserId,
        Counts = Counts(batch.Items),
        Items = batch.Items.OrderBy(item => item.Ordinal).Select(MapItem).ToArray(),
        AuditEntries = includeAudit
            ? batch.AuditEntries.OrderBy(item => item.Id).Select(MapAudit).ToArray()
            : [],
        CreatedAt = batch.CreatedAt,
        UpdatedAt = batch.UpdatedAt,
        StartedAt = batch.StartedAt,
        CompletedAt = batch.CompletedAt
    };

    private static ContentBatchListItemResponse MapListItem(ContentBatch batch) => new()
    {
        Id = batch.Id,
        CatalogName = batch.CatalogName,
        Status = batch.Status,
        CreatedByUserId = batch.CreatedByUserId,
        Counts = Counts(batch.Items),
        CreatedAt = batch.CreatedAt,
        UpdatedAt = batch.UpdatedAt
    };

    private static ContentBatchItemResponse MapItem(ContentBatchItem item) => new()
    {
        Id = item.Id,
        Ordinal = item.Ordinal,
        CatalogPath = item.CatalogPath,
        Slug = item.Slug,
        Title = item.Title,
        Action = item.Action,
        Status = item.Status,
        ContentHash = item.ContentHash,
        ProblemId = item.ProblemId ?? (item.Problem?.Id > 0 ? item.Problem.Id : null),
        RevisionId = item.RevisionId ?? item.Revision?.Id,
        SafeFailureCategory = item.SafeFailureCategory,
        SafeFailureMessage = item.SafeFailureMessage,
        UpdatedAt = item.UpdatedAt
    };

    private static ContentBatchAuditResponse MapAudit(ContentBatchAuditEntry item) => new()
    {
        Id = item.Id,
        AdminUserId = item.AdminUserId,
        ItemId = item.ItemId,
        ProblemId = item.ProblemId,
        RevisionId = item.RevisionId,
        Action = item.Action,
        Result = item.Result,
        SafeFailureCategory = item.SafeFailureCategory,
        CreatedAt = item.CreatedAt
    };

    private static ContentBatchCountsResponse Counts(IEnumerable<ContentBatchItem> items)
    {
        var values = items.ToArray();
        return new ContentBatchCountsResponse
        {
            Total = values.Length,
            Pending = values.Count(item => item.Status == ContentBatchItemStatus.Pending),
            Generating = values.Count(item =>
                item.Status is ContentBatchItemStatus.Generating or
                    ContentBatchItemStatus.Retrying),
            Ready = values.Count(item => item.Status == ContentBatchItemStatus.Ready),
            Failed = values.Count(item => item.Status == ContentBatchItemStatus.Failed),
            Published = values.Count(item => item.Status == ContentBatchItemStatus.Published),
            Skipped = values.Count(item => item.Status == ContentBatchItemStatus.Skipped)
        };
    }

    private static void Audit(
        ContentBatch batch,
        Guid adminUserId,
        string action,
        string result,
        ContentBatchItem? item = null,
        string? category = null)
    {
        batch.AuditEntries.Add(new ContentBatchAuditEntry
        {
            Batch = batch,
            BatchId = batch.Id,
            Item = item,
            ItemId = item?.Id,
            AdminUserId = adminUserId,
            ProblemId = item?.ProblemId ?? (item?.Problem?.Id > 0 ? item.Problem.Id : null),
            RevisionId = item?.RevisionId ?? item?.Revision?.Id,
            Action = action,
            Result = result,
            SafeFailureCategory = category,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void FailItem(
        ContentBatchItem item,
        string category,
        string message,
        DateTime now)
    {
        item.Status = ContentBatchItemStatus.Failed;
        item.SafeFailureCategory = Bound(category, 64);
        item.SafeFailureMessage = Bound(message, 1024);
        item.UpdatedAt = now;
        item.FinishedAt = now;
    }

    private static void Touch(ContentBatch batch)
    {
        batch.UpdatedAt = DateTime.UtcNow;
        batch.ConcurrencyToken = Guid.NewGuid();
    }

    private static void ValidateAdmin(Guid adminUserId)
    {
        if (adminUserId == Guid.Empty)
            throw new ForbiddenException("An Admin identity is required.");
    }

    private static string Bound(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value[..Math.Min(value.Length, maximum)];

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.NonBacktracking)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.NonBacktracking)]
    private static partial Regex HashPattern();

    [GeneratedRegex("^[A-Za-z]:", RegexOptions.NonBacktracking)]
    private static partial Regex WindowsDrivePath();

    private sealed class BatchItemFailureException(
        string category,
        string safeMessage) : Exception(safeMessage)
    {
        public string Category { get; } = category;
        public string SafeMessage { get; } = safeMessage;
    }
}
