using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.ContentTool.Publishing;

public sealed class ProblemPublicationService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ProblemPublicationService(AppDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ContentPublicationResult> PublishAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var problem = await GetProblemAsync(slug, cancellationToken);
        if (problem.Status == ProblemStatus.Archived)
        {
            throw new ContentPublicationConflictException(
                $"Archived problem '{problem.Slug}' cannot be published.");
        }

        if (problem.Status == ProblemStatus.Published)
        {
            return new ContentPublicationResult(
                problem.Id,
                problem.Slug,
                problem.Status,
                Changed: false);
        }

        ValidateForPublication(problem);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        problem.Status = ProblemStatus.Published;
        problem.PublishedAt = now;
        problem.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new ContentPublicationResult(
            problem.Id,
            problem.Slug,
            problem.Status,
            Changed: true);
    }

    public async Task<ContentPublicationResult> UnpublishAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var problem = await GetProblemAsync(slug, cancellationToken);
        if (problem.Status == ProblemStatus.Archived)
        {
            throw new ContentPublicationConflictException(
                $"Archived problem '{problem.Slug}' cannot be unpublished.");
        }

        if (problem.Status == ProblemStatus.Draft)
        {
            return new ContentPublicationResult(
                problem.Id,
                problem.Slug,
                problem.Status,
                Changed: false);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        problem.Status = ProblemStatus.Draft;
        problem.PublishedAt = null;
        problem.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new ContentPublicationResult(
            problem.Id,
            problem.Slug,
            problem.Status,
            Changed: true);
    }

    private async Task<Problem> GetProblemAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ContentPublicationConflictException("A problem slug is required.");

        var normalizedSlug = slug.Trim();
        var problem = await _context.Problems
            .Include(item => item.Samples)
            .Include(item => item.JudgeTestCases)
            .SingleOrDefaultAsync(item => item.Slug == normalizedSlug, cancellationToken);

        return problem ?? throw new ContentPublicationConflictException(
            $"Problem slug '{normalizedSlug}' does not exist.");
    }

    private static void ValidateForPublication(Problem problem)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(problem.Title)) errors.Add("a title is required");
        if (string.IsNullOrWhiteSpace(problem.StatementMarkdown))
            errors.Add("a statement is required");
        if (string.IsNullOrWhiteSpace(problem.ConstraintsMarkdown))
            errors.Add("constraints are required");
        if (problem.TimeLimitMs <= 0) errors.Add("the time limit must be positive");
        if (problem.MemoryLimitKb <= 0) errors.Add("the memory limit must be positive");
        if (problem.JudgeVersion <= 0) errors.Add("the judge version must be positive");
        if (problem.Samples.Count == 0) errors.Add("at least one public sample is required");
        if (problem.JudgeTestCases.Count == 0)
            errors.Add("at least one private judge case is required");
        if (problem.ExecutionMode == ProblemExecutionMode.Function)
        {
            var validationErrors = new List<string>();
            var signature = FunctionPackageValidator.ParseSignature(
                problem.FunctionSignatureJson,
                validationErrors);
            FunctionPackageValidator.ValidateAdapterTemplate(
                problem.FunctionAdapterTemplate,
                validationErrors);
            if (signature is not null && validationErrors.Count == 0)
            {
                foreach (var sample in problem.Samples)
                {
                    FunctionPackageValidator.ValidateCase(
                        signature,
                        sample.Input,
                        sample.ExpectedOutput,
                        $"Sample {sample.Ordinal}",
                        validationErrors);
                }

                foreach (var testCase in problem.JudgeTestCases)
                {
                    FunctionPackageValidator.ValidateCase(
                        signature,
                        testCase.Input,
                        testCase.ExpectedOutput,
                        $"Judge test case {testCase.Ordinal}",
                        validationErrors);
                }
            }

            if (validationErrors.Count > 0)
                errors.Add("the function configuration is invalid");
        }
        if (problem.ExecutionMode == ProblemExecutionMode.StdinStdout &&
            (problem.FunctionSignatureJson is not null ||
             problem.FunctionAdapterTemplate is not null))
        {
            errors.Add("stdin/stdout problems cannot contain function configuration");
        }
        if (!Enum.IsDefined(problem.ExecutionMode))
            errors.Add("the execution mode is invalid");

        if (errors.Count > 0)
        {
            throw new ContentPublicationConflictException(
                $"Problem '{problem.Slug}' cannot be published: {string.Join("; ", errors)}.");
        }
    }
}
