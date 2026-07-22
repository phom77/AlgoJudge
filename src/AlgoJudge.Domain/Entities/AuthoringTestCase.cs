namespace AlgoJudge.Domain.Entities;

public sealed class AuthoringTestCase
{
    public long Id { get; set; }
    public Guid RevisionId { get; set; }
    public int Ordinal { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Seed { get; set; }
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public string KilledWrongSolutionsJson { get; set; } = "[]";
    public ProblemAuthoringRevision Revision { get; set; } = null!;
}
