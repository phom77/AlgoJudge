using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace AlgoJudge.Application.Services;

public sealed class RunService : IRunService
{
    private readonly IRunRepository _runRepository;
    private readonly IProblemRepository _problemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RunService(
        IRunRepository runRepository,
        IProblemRepository problemRepository,
        IUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _runRepository = runRepository;
        _problemRepository = problemRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RunResponse> CreateAsync(
        string problemSlug,
        CreateRunRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateBaseRequest(problemSlug, request);
        var problem = await _problemRepository.GetPublishedBySlugAsync(
            problemSlug.Trim().ToLowerInvariant());
        if (problem is null)
            throw new ResourceNotFoundException($"Problem '{problemSlug}' was not found.");

        var input = problem.ExecutionMode switch
        {
            ProblemExecutionMode.StdinStdout => ValidateStdinInput(request),
            ProblemExecutionMode.Function => ValidateFunctionArguments(problem, request),
            _ => throw new InvalidOperationException(
                $"Problem {problem.Id} has an unsupported execution mode.")
        };
        if (Encoding.UTF8.GetByteCount(input) > RunContractLimits.MaxInputBytes)
        {
            throw new RequestValidationException(
                $"Run input must not exceed {RunContractLimits.MaxInputBytes} UTF-8 bytes.");
        }

        var run = new CodeRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProblemId = problem.Id,
            SourceCode = request.SourceCode,
            Language = request.Language,
            Input = input,
            Status = RunStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        await _runRepository.AddAsync(run, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(run);
    }

    public async Task<RunResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.GetByIdForUserAsync(id, userId, cancellationToken);
        if (run is not null)
            return Map(run);

        if (await _runRepository.ExistsAsync(id, cancellationToken))
            throw new ForbiddenException("You cannot access another user's run.");

        return null;
    }

    private static void ValidateBaseRequest(string problemSlug, CreateRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(problemSlug))
            throw new RequestValidationException("Problem slug is required.");
        if (!string.Equals(request.Language, "cpp17", StringComparison.Ordinal))
            throw new RequestValidationException("Language must be 'cpp17'.");
        if (string.IsNullOrWhiteSpace(request.SourceCode))
            throw new RequestValidationException("Source code is required.");
        if (Encoding.UTF8.GetByteCount(request.SourceCode) >
            SubmissionContractLimits.MaxSourceCodeBytes)
        {
            throw new RequestValidationException(
                $"Source code must not exceed {SubmissionContractLimits.MaxSourceCodeBytes} UTF-8 bytes.");
        }
    }

    private static string ValidateStdinInput(CreateRunRequest request)
    {
        if (request.Arguments.HasValue)
        {
            throw new RequestValidationException(
                "arguments is available only for Function problems.");
        }
        if (request.Input is null)
            throw new RequestValidationException("input is required for StdinStdout problems.");

        return request.Input;
    }

    private static string ValidateFunctionArguments(Problem problem, CreateRunRequest request)
    {
        if (request.Input is not null)
        {
            throw new RequestValidationException(
                "input is available only for StdinStdout problems.");
        }
        if (!request.Arguments.HasValue ||
            request.Arguments.Value.ValueKind != JsonValueKind.Object)
        {
            throw new RequestValidationException(
                "arguments must be a JSON object for Function problems.");
        }

        var signature = FunctionSignatureJsonSerializer.Deserialize(
            problem.FunctionSignatureJson ?? throw new InvalidOperationException(
                $"Function signature is missing for problem {problem.Id}."));
        var properties = request.Arguments.Value.EnumerateObject().ToArray();
        var actualNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            if (!actualNames.Add(property.Name))
                throw new RequestValidationException($"Duplicate function argument: {property.Name}.");
        }

        var expectedNames = signature.Parameters
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            var parameter = signature.Parameters.SingleOrDefault(item =>
                string.Equals(item.Name, property.Name, StringComparison.Ordinal));
            if (parameter is null)
                throw new RequestValidationException($"Unknown function argument: {property.Name}.");
            if (!FunctionValueJsonValidator.Matches(property.Value, parameter.Type))
            {
                throw new RequestValidationException(
                    $"Function argument {property.Name} must match type {parameter.Type}.");
            }
        }

        var missing = expectedNames.Except(actualNames, StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
            throw new RequestValidationException($"Function argument {missing} is required.");

        return NormalizeArguments(request.Arguments.Value, signature);
    }

    private static string NormalizeArguments(
        JsonElement arguments,
        FunctionSignature signature)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var parameter in signature.Parameters)
            {
                writer.WritePropertyName(parameter.Name);
                arguments.GetProperty(parameter.Name).WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static RunResponse Map(CodeRun run) => new()
    {
        Id = run.Id,
        ProblemId = run.ProblemId,
        Status = run.Status,
        Stdout = run.StandardOutput,
        Stderr = run.ErrorOutput,
        ExecutionTimeMs = run.ExecutionTimeMs,
        MemoryUsedKb = run.MemoryUsedKb,
        CreatedAt = run.CreatedAt,
        StartedAt = run.StartedAt,
        FinishedAt = run.FinishedAt
    };
}
