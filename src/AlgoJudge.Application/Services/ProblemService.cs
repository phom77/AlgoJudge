using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;

namespace AlgoJudge.Application.Services
{
    public class ProblemService : IProblemService
    {
        private readonly IProblemRepository _problemRepository;
        private readonly ISubmissionRepository _submissionRepository;

        public ProblemService(
            IProblemRepository problemRepository,
            ISubmissionRepository submissionRepository)
        {
            _problemRepository = problemRepository;
            _submissionRepository = submissionRepository;
        }

        public async Task<PagedResponse<ProblemListItemResponse>> GetProblemsAsync(
            ProblemListQuery query,
            Guid? userId)
        {
            if (query.Solved.HasValue && !userId.HasValue)
            {
                throw new RequestValidationException(
                    "The solved filter is available only to authenticated users.");
            }

            var tags = (query.Tags ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var page = await _problemRepository.GetPublishedPagedAsync(
                query.Search?.Trim(),
                query.Difficulty,
                tags,
                userId,
                query.Solved,
                query.PageNumber,
                query.PageSize);

            IReadOnlySet<int> solvedProblemIds = new HashSet<int>();
            if (userId.HasValue)
            {
                var solvedIds = await _submissionRepository.GetSolvedProblemIdsAsync(
                    userId.Value,
                    page.Items.Select(problem => problem.Id));
                solvedProblemIds = solvedIds.ToHashSet();
            }

            return new PagedResponse<ProblemListItemResponse>
            {
                Items = page.Items
                    .Select(problem => MapListItem(problem, userId, solvedProblemIds))
                    .ToArray(),
                TotalCount = page.TotalCount,
                PageNumber = page.PageNumber,
                PageSize = page.PageSize
            };
        }

        public async Task<ProblemDetailResponse?> GetProblemBySlugAsync(
            string slug,
            Guid? userId)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            var problem = await _problemRepository.GetPublishedBySlugAsync(
                slug.Trim().ToLowerInvariant());
            if (problem == null)
                return null;

            bool? isSolved = null;
            if (userId.HasValue)
            {
                isSolved = await _submissionRepository.HasAcceptedSubmissionAsync(
                    userId.Value,
                    problem.Id);
            }

            return new ProblemDetailResponse
            {
                Id = problem.Id,
                Slug = problem.Slug,
                Title = problem.Title,
                StatementMarkdown = problem.StatementMarkdown,
                ConstraintsMarkdown = problem.ConstraintsMarkdown,
                Difficulty = problem.Difficulty,
                TimeLimitMs = problem.TimeLimitMs,
                MemoryLimitKb = problem.MemoryLimitKb,
                JudgeVersion = problem.JudgeVersion,
                PublishedAt = problem.PublishedAt,
                Tags = MapTags(problem),
                Samples = problem.Samples
                    .OrderBy(sample => sample.Ordinal)
                    .Select(sample => new ProblemSampleResponse
                    {
                        Input = sample.Input,
                        ExpectedOutput = sample.ExpectedOutput,
                        Explanation = sample.Explanation,
                        Ordinal = sample.Ordinal
                    })
                    .ToArray(),
                IsSolved = isSolved
            };
        }

        private static ProblemListItemResponse MapListItem(
            Problem problem,
            Guid? userId,
            IReadOnlySet<int> solvedProblemIds)
        {
            return new ProblemListItemResponse
            {
                Id = problem.Id,
                Slug = problem.Slug,
                Title = problem.Title,
                Difficulty = problem.Difficulty,
                Tags = MapTags(problem),
                IsSolved = userId.HasValue
                    ? solvedProblemIds.Contains(problem.Id)
                    : null
            };
        }

        private static TagResponse[] MapTags(Problem problem)
        {
            return problem.Tags
                .Select(problemTag => problemTag.Tag)
                .OrderBy(tag => tag.Name)
                .Select(tag => new TagResponse
                {
                    Slug = tag.Slug,
                    Name = tag.Name
                })
                .ToArray();
        }
    }
}
