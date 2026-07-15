namespace AlgoJudge.Domain.Entities
{
    public class JudgeTestCase
    {
        public int Id { get; set; }
        public int ProblemId { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public int Ordinal { get; set; }
        public Problem Problem { get; set; } = null!;
    }
}
