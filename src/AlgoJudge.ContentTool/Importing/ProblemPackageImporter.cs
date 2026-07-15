using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.ContentTool.Importing;

public sealed class ProblemPackageImporter
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ProblemPackageImporter(AppDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ContentImportResult> ImportAsync(
        ProblemPackage package,
        bool replace,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        var problem = await _context.Problems
            .Include(existing => existing.Samples)
            .Include(existing => existing.JudgeTestCases)
            .Include(existing => existing.Tags)
            .SingleOrDefaultAsync(
                existing => existing.Slug == package.Metadata.Slug,
                cancellationToken);

        var isReplacement = problem is not null;
        if (problem is not null && !replace)
        {
            throw new ContentImportConflictException(
                $"Problem slug '{package.Metadata.Slug}' already exists. " +
                "Use --replace only when the existing problem is Draft.");
        }

        if (problem is not null && problem.Status != ProblemStatus.Draft)
        {
            throw new ContentImportConflictException(
                $"Problem slug '{package.Metadata.Slug}' is {problem.Status} and cannot be replaced.");
        }

        var requestedTags = package.Metadata.Tags.ToArray();
        var requestedTagSlugs = requestedTags.Select(tag => tag.Slug).ToArray();
        var tagsBySlug = requestedTagSlugs.Length == 0
            ? new Dictionary<string, Tag>(StringComparer.Ordinal)
            : await _context.Tags
                .Where(tag => requestedTagSlugs.Contains(tag.Slug))
                .ToDictionaryAsync(tag => tag.Slug, StringComparer.Ordinal, cancellationToken);

        foreach (var requestedTag in requestedTags)
        {
            if (tagsBySlug.TryGetValue(requestedTag.Slug, out var existingTag))
            {
                if (!string.Equals(existingTag.Name, requestedTag.Name, StringComparison.Ordinal))
                {
                    throw new ContentImportConflictException(
                        $"Tag slug '{requestedTag.Slug}' already uses a different display name.");
                }

                continue;
            }

            var newTag = new Tag
            {
                Slug = requestedTag.Slug,
                Name = requestedTag.Name
            };
            tagsBySlug.Add(newTag.Slug, newTag);
            _context.Tags.Add(newTag);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (problem is null)
        {
            problem = new Problem
            {
                Slug = package.Metadata.Slug,
                Status = ProblemStatus.Draft,
                JudgeVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Problems.Add(problem);
        }
        else
        {
            _context.ProblemSamples.RemoveRange(problem.Samples);
            _context.JudgeTestCases.RemoveRange(problem.JudgeTestCases);
            _context.ProblemTags.RemoveRange(problem.Tags);
            await _context.SaveChangesAsync(cancellationToken);

            problem.JudgeVersion = checked(problem.JudgeVersion + 1);
            problem.UpdatedAt = now;
            problem.PublishedAt = null;
        }

        problem.Title = package.Metadata.Title;
        problem.StatementMarkdown = package.StatementMarkdown;
        problem.ConstraintsMarkdown = package.ConstraintsMarkdown;
        problem.Difficulty = package.Metadata.Difficulty;
        problem.TimeLimitMs = package.Metadata.TimeLimitMs;
        problem.MemoryLimitKb = package.Metadata.MemoryLimitKb;

        foreach (var sample in package.Samples)
        {
            problem.Samples.Add(new ProblemSample
            {
                Input = sample.Input,
                ExpectedOutput = sample.ExpectedOutput,
                Explanation = sample.Explanation,
                Ordinal = sample.Ordinal
            });
        }

        foreach (var testCase in package.JudgeTestCases)
        {
            problem.JudgeTestCases.Add(new JudgeTestCase
            {
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                Ordinal = testCase.Ordinal
            });
        }

        foreach (var requestedTag in requestedTags)
        {
            problem.Tags.Add(new ProblemTag
            {
                Tag = tagsBySlug[requestedTag.Slug]
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ContentImportResult(
            problem.Id,
            problem.Slug,
            isReplacement,
            problem.JudgeVersion,
            package.Samples.Count,
            package.JudgeTestCases.Count);
    }
}
