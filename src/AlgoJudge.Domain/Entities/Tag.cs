namespace AlgoJudge.Domain.Entities
{
    public class Tag
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ICollection<ProblemTag> Problems { get; set; } = new List<ProblemTag>();
    }
}
