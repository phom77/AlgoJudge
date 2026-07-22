using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Entities;

public sealed class ProblemAuthoringRevision
{
    public Guid Id { get; set; }
    public int ProblemId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int RevisionNumber { get; set; }
    public AuthoringRevisionStatus Status { get; set; } = AuthoringRevisionStatus.Draft;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string StatementMarkdown { get; set; } = string.Empty;
    public string ConstraintsMarkdown { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; }
    public int TimeLimitMs { get; set; }
    public int MemoryLimitKb { get; set; }
    public string SamplesJson { get; set; } = "[]";
    public string DefinitionJson { get; set; } = "{}";
    public string DefinitionSha256 { get; set; } = string.Empty;
    public string? CandidateSuiteSha256 { get; set; }
    public string? CandidateToolchain { get; set; }
    public string? CandidateStatisticsJson { get; set; }
    public int? CandidateCaseCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public Problem Problem { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public ICollection<ContentGenerationJob> GenerationJobs { get; set; } = [];
    public ICollection<AuthoringTestCase> CandidateTestCases { get; set; } = [];
}
