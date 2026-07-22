using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Services;

public sealed partial class ProblemAuthoringService : IProblemAuthoringService
{
    private static readonly HashSet<string> Cpp17Keywords = new(
        [
            "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool",
            "break", "case", "catch", "char", "char16_t", "char32_t", "class", "compl",
            "concept", "const", "constexpr", "const_cast", "continue", "co_await", "co_return",
            "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else",
            "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto",
            "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not",
            "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected", "public",
            "register", "reinterpret_cast", "requires", "return", "short", "signed", "sizeof",
            "static", "static_assert", "static_cast", "struct", "switch", "template", "this",
            "thread_local", "throw", "true", "try", "typedef", "typeid", "typename", "union",
            "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq"
        ],
        StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly IProblemAuthoringRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProblemAuthoringService(IProblemAuthoringRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProblemDraftResponse> CreateDraftAsync(
        Guid ownerUserId,
        CreateProblemDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(ownerUserId);
        ValidateMetadata(request.Slug, request.Title, request.StatementMarkdown,
            request.ConstraintsMarkdown, request.TimeLimitMs, request.MemoryLimitKb, request.Samples);
        var definition = EmptyDefinition();
        var definitionJson = SerializeDefinition(definition);
        var now = DateTime.UtcNow;
        var problem = new Problem
        {
            Slug = request.Slug.Trim(),
            Title = request.Title.Trim(),
            StatementMarkdown = request.StatementMarkdown,
            ConstraintsMarkdown = request.ConstraintsMarkdown,
            Difficulty = request.Difficulty,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitKb = request.MemoryLimitKb,
            ExecutionMode = ProblemExecutionMode.Function,
            FunctionSignatureJson = FunctionSignatureJsonSerializer.Serialize(definition.FunctionSignature),
            Status = ProblemStatus.Draft,
            JudgeVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var revision = CreateRevision(problem, ownerUserId, 1, request.Slug, request.Title,
            request.StatementMarkdown, request.ConstraintsMarkdown, request.Difficulty,
            request.TimeLimitMs, request.MemoryLimitKb, request.Samples, definitionJson, now);
        await _repository.AddProblemAsync(problem, cancellationToken);
        await _repository.AddRevisionAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ProblemDraftResponse> CreateNextRevisionAsync(
        Guid ownerUserId,
        int problemId,
        CancellationToken cancellationToken = default)
    {
        var latest = await _repository.GetLatestOwnedRevisionAsync(problemId, ownerUserId, cancellationToken)
            ?? throw new ResourceNotFoundException("Problem authoring revision was not found.");
        if (latest.Status != AuthoringRevisionStatus.Published)
            throw new ConflictException("The problem already has an editable revision.");
        var now = DateTime.UtcNow;
        var revision = CreateRevision(latest.Problem, ownerUserId, latest.RevisionNumber + 1,
            latest.Slug, latest.Title, latest.StatementMarkdown, latest.ConstraintsMarkdown,
            latest.Difficulty, latest.TimeLimitMs, latest.MemoryLimitKb,
            DeserializeSamples(latest.SamplesJson), latest.DefinitionJson, now);
        await _repository.AddRevisionAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ProblemDraftResponse> GetDraftAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default) =>
        Map(await GetOwnedAsync(ownerUserId, revisionId, false, cancellationToken));

    public async Task<ProblemDraftResponse> UpdateMetadataAsync(
        Guid ownerUserId, Guid revisionId, UpdateProblemDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var revision = await GetEditableAsync(ownerUserId, revisionId, cancellationToken);
        ValidateMetadata(request.Slug, request.Title, request.StatementMarkdown,
            request.ConstraintsMarkdown, request.TimeLimitMs, request.MemoryLimitKb, request.Samples);
        revision.Slug = request.Slug.Trim();
        revision.Title = request.Title.Trim();
        revision.StatementMarkdown = request.StatementMarkdown;
        revision.ConstraintsMarkdown = request.ConstraintsMarkdown;
        revision.Difficulty = request.Difficulty;
        revision.TimeLimitMs = request.TimeLimitMs;
        revision.MemoryLimitKb = request.MemoryLimitKb;
        revision.SamplesJson = JsonSerializer.Serialize(request.Samples, JsonOptions);
        revision.ConcurrencyToken = Guid.NewGuid();
        await InvalidateCandidateAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ProblemDraftResponse> UpdateSignatureAsync(
        Guid ownerUserId, Guid revisionId, UpdateFunctionSignatureRequest request,
        CancellationToken cancellationToken = default)
    {
        var revision = await GetEditableAsync(ownerUserId, revisionId, cancellationToken);
        ValidateSignature(request.Signature);
        var current = DeserializeDefinition(revision.DefinitionJson);
        var updated = Copy(current, functionSignature: request.Signature);
        SetDefinition(revision, updated);
        revision.ConcurrencyToken = Guid.NewGuid();
        await InvalidateCandidateAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ProblemDraftResponse> UpdateHandwrittenCasesAsync(
        Guid ownerUserId, Guid revisionId, UpdateHandwrittenCasesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Cases);
        if (request.Cases.Count > 500)
            throw new RequestValidationException("At most 500 handwritten cases are allowed.");
        var revision = await GetEditableAsync(ownerUserId, revisionId, cancellationToken);
        var current = DeserializeDefinition(revision.DefinitionJson);
        ValidateHandwrittenCases(request.Cases, current.FunctionSignature);
        var updated = Copy(current, handwrittenCases: request.Cases);
        SetDefinition(revision, updated);
        revision.ConcurrencyToken = Guid.NewGuid();
        await InvalidateCandidateAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ProblemDraftResponse> UpdateSourcesAsync(
        Guid ownerUserId, Guid revisionId, UpdateAuthoringSourcesRequest request,
        CancellationToken cancellationToken = default)
    {
        var revision = await GetEditableAsync(ownerUserId, revisionId, cancellationToken);
        ValidateSources(request);
        var current = DeserializeDefinition(revision.DefinitionJson);
        var updated = Copy(current, generator: request.Generator,
            inputValidator: request.InputValidator, referenceSolution: request.ReferenceSolution,
            wrongSolutions: request.WrongSolutions);
        SetDefinition(revision, updated);
        revision.ConcurrencyToken = Guid.NewGuid();
        await InvalidateCandidateAsync(revision, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(revision);
    }

    public async Task<ContentGenerationStatusResponse> StartGenerationAsync(
        Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        var revision = await GetEditableAsync(ownerUserId, revisionId, cancellationToken);
        var definition = DeserializeDefinition(revision.DefinitionJson);
        ValidateForGeneration(revision, definition);
        var existing = await _repository.GetLatestJobAsync(revision.Id, cancellationToken);
        if (existing?.Status is ContentGenerationJobStatus.Pending or ContentGenerationJobStatus.Running)
            throw new ConflictException("A generation job is already active for this revision.");
        await InvalidateCandidateAsync(revision, cancellationToken);
        var job = new ContentGenerationJob
        {
            Id = Guid.NewGuid(),
            RevisionId = revision.Id,
            Status = ContentGenerationJobStatus.Pending,
            DefinitionSnapshotJson = revision.DefinitionJson,
            DefinitionSha256 = revision.DefinitionSha256,
            TimeLimitMs = revision.TimeLimitMs,
            MemoryLimitKb = revision.MemoryLimitKb,
            CreatedAt = DateTime.UtcNow
        };
        revision.GenerationJobs.Add(job);
        revision.Status = AuthoringRevisionStatus.Generating;
        revision.ConcurrencyToken = Guid.NewGuid();
        revision.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(job, revision.Status);
    }

    public async Task<ContentGenerationStatusResponse> GetGenerationStatusAsync(
        Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        var revision = await GetOwnedAsync(ownerUserId, revisionId, false, cancellationToken);
        var job = await _repository.GetLatestJobAsync(revision.Id, cancellationToken)
            ?? throw new ResourceNotFoundException("No generation job exists for this revision.");
        return Map(job, revision.Status);
    }

    public async Task<GeneratedSuiteReviewResponse> GetSuiteReviewAsync(
        Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        var revision = await GetOwnedAsync(ownerUserId, revisionId, true, cancellationToken);
        if (revision.Status is not (AuthoringRevisionStatus.Ready or AuthoringRevisionStatus.Published) ||
            revision.CandidateStatisticsJson is null || revision.CandidateSuiteSha256 is null ||
            revision.CandidateCaseCount is null)
            throw new ConflictException("The revision has no generated suite ready for review.");
        var statistics = JsonSerializer.Deserialize<SuiteStatistics>(revision.CandidateStatisticsJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored suite statistics are invalid.");
        return new GeneratedSuiteReviewResponse
        {
            RevisionId = revision.Id,
            SuiteSha256 = revision.CandidateSuiteSha256,
            TestCaseCount = revision.CandidateCaseCount.Value,
            CasesByGroup = statistics.CasesByGroup,
            WrongSolutionCount = statistics.WrongSolutionCount,
            KilledCaseCountByWrongSolution = statistics.KilledCaseCountByWrongSolution,
            SurvivingWrongSolutions = statistics.SurvivingWrongSolutions,
            Toolchain = revision.CandidateToolchain ?? string.Empty
        };
    }

    public async Task PublishAsync(Guid ownerUserId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedAsync(ownerUserId, revisionId, true, cancellationToken);
        if (!await _repository.PublishAsync(revisionId, ownerUserId, cancellationToken))
            throw new ConflictException("Only a Ready revision with a complete candidate suite can be published.");
    }

    private async Task<ProblemAuthoringRevision> GetOwnedAsync(Guid ownerUserId, Guid revisionId, bool includeCandidate, CancellationToken token)
    {
        ValidateOwner(ownerUserId);
        return await _repository.GetOwnedRevisionAsync(revisionId, ownerUserId, includeCandidate, token)
            ?? throw new ResourceNotFoundException("Problem authoring revision was not found.");
    }

    private async Task<ProblemAuthoringRevision> GetEditableAsync(Guid ownerUserId, Guid revisionId, CancellationToken token)
    {
        var revision = await GetOwnedAsync(ownerUserId, revisionId, false, token);
        if (revision.Status is AuthoringRevisionStatus.Generating or AuthoringRevisionStatus.Published)
            throw new ConflictException("The revision is not editable in its current state.");
        return revision;
    }

    private async Task InvalidateCandidateAsync(ProblemAuthoringRevision revision, CancellationToken token)
    {
        if (revision.Status == AuthoringRevisionStatus.Ready)
            await _repository.DeleteCandidateCasesAsync(revision.Id, token);
        revision.Status = AuthoringRevisionStatus.Draft;
        revision.CandidateSuiteSha256 = null;
        revision.CandidateToolchain = null;
        revision.CandidateStatisticsJson = null;
        revision.CandidateCaseCount = null;
        revision.UpdatedAt = DateTime.UtcNow;
    }

    private static ProblemAuthoringRevision CreateRevision(
        Problem problem, Guid owner, int number, string slug, string title, string statement,
        string constraints, DifficultyLevel difficulty, int timeLimit, int memoryLimit,
        IReadOnlyList<ProblemSampleRequest> samples, string definitionJson, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            Problem = problem,
            OwnerUserId = owner,
            RevisionNumber = number,
            Status = AuthoringRevisionStatus.Draft,
            Slug = slug.Trim(),
            Title = title.Trim(),
            StatementMarkdown = statement,
            ConstraintsMarkdown = constraints,
            Difficulty = difficulty,
            TimeLimitMs = timeLimit,
            MemoryLimitKb = memoryLimit,
            SamplesJson = JsonSerializer.Serialize(samples, JsonOptions),
            DefinitionJson = definitionJson,
            DefinitionSha256 = Hash(definitionJson),
            ConcurrencyToken = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };

    private static ProblemAuthoringDefinition EmptyDefinition() => new()
    {
        SchemaVersion = 1,
        ExecutionMode = ProblemExecutionMode.Function,
        Generator = new GeneratorSourceDefinition { Language = "csharp", SdkVersion = 1 },
        InputValidator = new GeneratorSourceDefinition { Language = "csharp", SdkVersion = 1 },
        ReferenceSolution = new FunctionSourceDefinition { Language = "cpp17" }
    };

    private static ProblemAuthoringDefinition Copy(ProblemAuthoringDefinition value,
        FunctionSignature? functionSignature = null,
        IReadOnlyList<HandwrittenCaseDefinition>? handwrittenCases = null,
        GeneratorSourceDefinition? generator = null,
        GeneratorSourceDefinition? inputValidator = null,
        FunctionSourceDefinition? referenceSolution = null,
        IReadOnlyList<WrongSolutionDefinition>? wrongSolutions = null) => new()
        {
            SchemaVersion = value.SchemaVersion,
            ExecutionMode = value.ExecutionMode,
            FunctionSignature = functionSignature ?? value.FunctionSignature,
            HandwrittenCases = handwrittenCases ?? value.HandwrittenCases,
            Generator = generator ?? value.Generator,
            InputValidator = inputValidator ?? value.InputValidator,
            ReferenceSolution = referenceSolution ?? value.ReferenceSolution,
            WrongSolutions = wrongSolutions ?? value.WrongSolutions
        };

    private static void SetDefinition(ProblemAuthoringRevision revision, ProblemAuthoringDefinition definition)
    {
        revision.DefinitionJson = SerializeDefinition(definition);
        revision.DefinitionSha256 = Hash(revision.DefinitionJson);
    }

    private static ProblemDraftResponse Map(ProblemAuthoringRevision revision) => new()
    {
        RevisionId = revision.Id,
        ProblemId = revision.ProblemId,
        RevisionNumber = revision.RevisionNumber,
        Status = revision.Status,
        Slug = revision.Slug,
        Title = revision.Title,
        StatementMarkdown = revision.StatementMarkdown,
        ConstraintsMarkdown = revision.ConstraintsMarkdown,
        Difficulty = revision.Difficulty,
        TimeLimitMs = revision.TimeLimitMs,
        MemoryLimitKb = revision.MemoryLimitKb,
        Samples = DeserializeSamples(revision.SamplesJson),
        Definition = DeserializeDefinition(revision.DefinitionJson),
        UpdatedAt = revision.UpdatedAt
    };

    private static ContentGenerationStatusResponse Map(ContentGenerationJob job, AuthoringRevisionStatus revisionStatus) => new()
    {
        JobId = job.Id,
        RevisionId = job.RevisionId,
        JobStatus = job.Status,
        RevisionStatus = revisionStatus,
        AttemptCount = job.AttemptCount,
        ErrorCode = job.ErrorCode,
        ErrorMessage = job.ErrorMessage,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        FinishedAt = job.FinishedAt
    };

    private static void ValidateOwner(Guid owner) { if (owner == Guid.Empty) throw new ForbiddenException("A maintainer identity is required."); }
    private static void ValidateMetadata(string slug, string title, string statement, string constraints,
        int timeLimit, int memoryLimit, IReadOnlyList<ProblemSampleRequest> samples)
    {
        if (slug?.Length > 160 || !SlugPattern().IsMatch(slug ?? string.Empty) || string.IsNullOrWhiteSpace(title) || title.Length > 255 ||
            string.IsNullOrWhiteSpace(statement) || string.IsNullOrWhiteSpace(constraints) ||
            Encoding.UTF8.GetByteCount(statement) > 1024 * 1024 || Encoding.UTF8.GetByteCount(constraints) > 256 * 1024 ||
            timeLimit is < 100 or > 10_000 || memoryLimit is < 16_384 or > 1_048_576)
            throw new RequestValidationException("Problem draft metadata is invalid.");
        if (samples is null || samples.Count is < 1 or > 20 || samples.Any(sample =>
                sample is null || string.IsNullOrWhiteSpace(sample.Input) || string.IsNullOrWhiteSpace(sample.ExpectedOutput) ||
                Encoding.UTF8.GetByteCount(sample.Input) > 1024 * 1024 || Encoding.UTF8.GetByteCount(sample.ExpectedOutput) > 1024 * 1024))
            throw new RequestValidationException("A draft requires between 1 and 20 complete public samples.");
    }

    private static void ValidateSignature(FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        if (!CppIdentifierPattern().IsMatch(signature.ClassName) || Cpp17Keywords.Contains(signature.ClassName) ||
            !CppIdentifierPattern().IsMatch(signature.MethodName) || Cpp17Keywords.Contains(signature.MethodName) ||
            signature.Parameters is null || signature.Parameters.Count > 16 || signature.Parameters.Any(parameter =>
                parameter is null || !CppIdentifierPattern().IsMatch(parameter.Name) || Cpp17Keywords.Contains(parameter.Name)) ||
            signature.Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() != signature.Parameters.Count)
            throw new RequestValidationException("Function signature is invalid.");
    }

    private static void ValidateForGeneration(ProblemAuthoringRevision revision, ProblemAuthoringDefinition definition)
    {
        ValidateSignature(definition.FunctionSignature);
        if (definition.SchemaVersion != 1 || definition.ExecutionMode != ProblemExecutionMode.Function ||
            definition.HandwrittenCases.Count == 0 ||
            definition.Generator is not { Language: "csharp", SdkVersion: 1 } || string.IsNullOrWhiteSpace(definition.Generator.Source) ||
            definition.InputValidator is not { Language: "csharp", SdkVersion: 1 } || string.IsNullOrWhiteSpace(definition.InputValidator.Source) ||
            definition.ReferenceSolution.Language != "cpp17" || string.IsNullOrWhiteSpace(definition.ReferenceSolution.Source) ||
            definition.WrongSolutions.Any(item => item.Language != "cpp17" || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Source)))
            throw new RequestValidationException("The authoring definition is incomplete or invalid.");
        ValidateHandwrittenCases(definition.HandwrittenCases, definition.FunctionSignature);
        ValidateSources(new UpdateAuthoringSourcesRequest
        {
            Generator = definition.Generator,
            InputValidator = definition.InputValidator,
            ReferenceSolution = definition.ReferenceSolution,
            WrongSolutions = definition.WrongSolutions
        });
        ValidateMetadata(revision.Slug, revision.Title, revision.StatementMarkdown, revision.ConstraintsMarkdown,
            revision.TimeLimitMs, revision.MemoryLimitKb, DeserializeSamples(revision.SamplesJson));
        ValidateFunctionSamples(DeserializeSamples(revision.SamplesJson), definition.FunctionSignature);
    }

    private static void ValidateSources(UpdateAuthoringSourcesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Generator is null || request.InputValidator is null ||
            request.ReferenceSolution is null || request.WrongSolutions is null ||
            request.WrongSolutions.Any(item => item is null))
            throw new RequestValidationException("Authoring sources are required.");
        var sources = new[]
        {
            request.Generator.Source,
            request.InputValidator.Source,
            request.ReferenceSolution.Source
        }.Concat(request.WrongSolutions.Select(item => item.Source));
        if (request.WrongSolutions.Count > 50 || sources.Any(source =>
                source is null || Encoding.UTF8.GetByteCount(source) > 1024 * 1024))
            throw new RequestValidationException("Authoring source limits were exceeded.");
        if (request.WrongSolutions.Any(item => item.Name.Length > 160 || !SlugPattern().IsMatch(item.Name)))
            throw new RequestValidationException("Wrong-solution names are invalid.");
        if (request.WrongSolutions.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() !=
            request.WrongSolutions.Count)
            throw new RequestValidationException("Wrong-solution names must be unique.");
    }

    private static void ValidateHandwrittenCases(
        IReadOnlyList<HandwrittenCaseDefinition> cases,
        FunctionSignature signature)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cases)
        {
            if (item is null || item.Name.Length > 160 || !SlugPattern().IsMatch(item.Name) || item.Group != "handwritten" ||
                !names.Add(item.Name) || item.Arguments.ValueKind != JsonValueKind.Object ||
                item.Arguments.EnumerateObject().Count() != signature.Parameters.Count ||
                signature.Parameters.Any(parameter =>
                    !item.Arguments.TryGetProperty(parameter.Name, out var value) ||
                    !FunctionValueJsonValidator.Matches(value, parameter.Type)))
                throw new RequestValidationException("Handwritten cases do not match the Function signature.");
        }
    }

    private static void ValidateFunctionSamples(
        IReadOnlyList<ProblemSampleRequest> samples,
        FunctionSignature signature)
    {
        foreach (var sample in samples)
        {
            try
            {
                using var input = JsonDocument.Parse(sample.Input);
                using var output = JsonDocument.Parse(sample.ExpectedOutput);
                if (input.RootElement.ValueKind != JsonValueKind.Object ||
                    input.RootElement.EnumerateObject().Count() != signature.Parameters.Count ||
                    signature.Parameters.Any(parameter =>
                        !input.RootElement.TryGetProperty(parameter.Name, out var value) ||
                        !FunctionValueJsonValidator.Matches(value, parameter.Type)) ||
                    !FunctionValueJsonValidator.Matches(output.RootElement, signature.ReturnType))
                    throw new JsonException();
            }
            catch (JsonException)
            {
                throw new RequestValidationException("Public samples do not match the Function signature.");
            }
        }
    }

    private static string SerializeDefinition(ProblemAuthoringDefinition definition) => JsonSerializer.Serialize(definition, JsonOptions);
    private static ProblemAuthoringDefinition DeserializeDefinition(string json) =>
        JsonSerializer.Deserialize<ProblemAuthoringDefinition>(json, JsonOptions) ?? throw new InvalidOperationException("Stored authoring definition is invalid.");
    private static IReadOnlyList<ProblemSampleRequest> DeserializeSamples(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<ProblemSampleRequest>>(json, JsonOptions) ?? [];
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class SuiteStatistics
    {
        public IReadOnlyDictionary<string, int> CasesByGroup { get; init; } = new Dictionary<string, int>();
        public int WrongSolutionCount { get; init; }
        public IReadOnlyDictionary<string, int> KilledCaseCountByWrongSolution { get; init; } =
            new Dictionary<string, int>();
        public IReadOnlyList<string> SurvivingWrongSolutions { get; init; } = [];
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CppIdentifierPattern();
}
