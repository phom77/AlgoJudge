namespace AlgoJudge.Domain.Entities
{
    public class ProblemTag
    {
        public int ProblemId { get; set; }
        public int TagId { get; set; }
        public Problem Problem { get; set; } = null!;
        public Tag Tag { get; set; } = null!;
    }
}
