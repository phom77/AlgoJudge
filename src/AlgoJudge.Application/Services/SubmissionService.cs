using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AutoMapper;
using System.Text;

namespace AlgoJudge.Application.Services;

public sealed class SubmissionService : ISubmissionService
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IProblemRepository _problemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubmissionService(
        ISubmissionRepository submissionRepository,
        IProblemRepository problemRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _submissionRepository = submissionRepository;
        _problemRepository = problemRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResponse<SubmissionResponse>> GetHistoryAsync(
        Guid userId,
        SubmissionHistoryQuery query)
    {
        ValidatePagination(query.PageNumber, query.PageSize);
        if (query.ProblemId is <= 0)
            throw new RequestValidationException("Problem ID must be greater than zero.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new RequestValidationException("Submission status is invalid.");

        var pagedEntities = await _submissionRepository.GetPagedAsync(
            userId,
            query.ProblemId,
            query.Status,
            query.PageNumber,
            query.PageSize);

        return new PagedResponse<SubmissionResponse>
        {
            Items = _mapper.Map<IReadOnlyCollection<SubmissionResponse>>(
                pagedEntities.Items),
            TotalCount = pagedEntities.TotalCount,
            PageNumber = pagedEntities.PageNumber,
            PageSize = pagedEntities.PageSize
        };
    }

    public async Task<SubmissionResponse?> GetSubmissionByIdAsync(
        Guid id,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdForUserAsync(
            id,
            requesterId,
            cancellationToken);
        if (submission is not null)
            return _mapper.Map<SubmissionResponse>(submission);

        if (await _submissionRepository.ExistsAsync(id, cancellationToken))
        {
            throw new ForbiddenException(
                "You cannot access another user's submission.");
        }

        return null;
    }

    public async Task<SubmissionResponse> SubmitCodeAsync(
        CreateSubmissionRequest request,
        Guid userId)
    {
        ValidateSubmissionRequest(request);

        var problem = await _problemRepository.GetByIdAsync(request.ProblemId);
        if (problem == null)
        {
            throw new RequestValidationException(
                $"Problem with ID {request.ProblemId} does not exist.");
        }

        if (problem.Status != ProblemStatus.Published)
        {
            throw new RequestValidationException(
                "Submissions are accepted only for published problems.");
        }

        var submission = _mapper.Map<Submission>(request);
        submission.UserId = userId;
        submission.SystemTestSuiteVersion = problem.JudgeVersion;
        submission.Status = SubmissionStatus.Pending;
        submission.CreatedAt = DateTime.UtcNow;
        submission.ExecutionTime = 0;
        submission.MemoryUsed = 0;

        await _submissionRepository.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<SubmissionResponse>(submission);
    }

    private static void ValidateSubmissionRequest(CreateSubmissionRequest request)
    {
        if (request.ProblemId < 1)
            throw new RequestValidationException("Problem ID must be greater than zero.");

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

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new RequestValidationException("Page number must be at least 1.");
        if (pageSize is < 1 or > 100)
            throw new RequestValidationException("Page size must be between 1 and 100.");
    }
}
