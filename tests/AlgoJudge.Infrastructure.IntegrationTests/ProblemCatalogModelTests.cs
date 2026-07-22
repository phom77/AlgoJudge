using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AlgoJudge.Infrastructure.IntegrationTests;

public class ProblemCatalogModelTests
{
    [Fact]
    public void PublicSamplesAndJudgeTestCasesUseSeparateTables()
    {
        using var context = CreateContext();

        var sampleType = context.Model.FindEntityType(typeof(ProblemSample))
            ?? throw new InvalidOperationException("ProblemSample is missing from the EF model.");
        var judgeTestType = context.Model.FindEntityType(typeof(JudgeTestCase))
            ?? throw new InvalidOperationException("JudgeTestCase is missing from the EF model.");

        Assert.Equal("ProblemSamples", sampleType.GetTableName());
        Assert.Equal("JudgeTestCases", judgeTestType.GetTableName());
        Assert.NotEqual(sampleType.GetTableName(), judgeTestType.GetTableName());
    }

    [Fact]
    public void ProblemAndTagSlugsAreUnique()
    {
        using var context = CreateContext();

        Assert.Contains(
            context.Model.FindEntityType(typeof(Problem))!.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name)
                         .SequenceEqual([nameof(Problem.Slug)]));

        Assert.Contains(
            context.Model.FindEntityType(typeof(Tag))!.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Select(property => property.Name)
                         .SequenceEqual([nameof(Tag.Slug)]));
    }

    [Fact]
    public void ProblemExecutionModeConfigurationUsesJsonbAndDatabaseChecks()
    {
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var problemType = designTimeModel.FindEntityType(typeof(Problem))!;

        Assert.Equal(
            "jsonb",
            problemType.FindProperty(nameof(Problem.FunctionSignatureJson))!.GetColumnType());
        Assert.Equal(
            ProblemExecutionMode.StdinStdout,
            problemType.FindProperty(nameof(Problem.ExecutionMode))!.GetDefaultValue());
        Assert.Contains(
            problemType.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Problem_ExecutionMode");
        Assert.Contains(
            problemType.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Problem_FunctionConfiguration");
    }

    [Fact]
    public void AuthoringCandidatesAndPublishedJudgeTestsUseSeparateTables()
    {
        using var context = CreateContext();
        var revision = context.Model.FindEntityType(typeof(ProblemAuthoringRevision))!;
        var candidate = context.Model.FindEntityType(typeof(AuthoringTestCase))!;
        var job = context.Model.FindEntityType(typeof(ContentGenerationJob))!;

        Assert.Equal("ProblemAuthoringRevisions", revision.GetTableName());
        Assert.Equal("AuthoringTestCases", candidate.GetTableName());
        Assert.Equal("ContentGenerationJobs", job.GetTableName());
        Assert.NotEqual(candidate.GetTableName(), context.Model.FindEntityType(typeof(JudgeTestCase))!.GetTableName());
        Assert.Equal("jsonb", revision.FindProperty(nameof(ProblemAuthoringRevision.DefinitionJson))!.GetColumnType());
        Assert.Contains(job.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"Status\" IN (0, 1)");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=algojudge_model_tests;Username=test;Password=test")
            .Options;

        return new AppDbContext(options);
    }
}
