using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AlgoJudge.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<CodeRun> CodeRuns { get; set; }
        public DbSet<ProblemSample> ProblemSamples { get; set; }
        public DbSet<JudgeTestCase> JudgeTestCases { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ProblemTag> ProblemTags { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ProblemAuthoringRevision> ProblemAuthoringRevisions { get; set; }
        public DbSet<ContentGenerationJob> ContentGenerationJobs { get; set; }
        public DbSet<AuthoringTestCase> AuthoringTestCases { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
