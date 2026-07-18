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

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=algojudge_model_tests;Username=test;Password=test")
            .Options;

        return new AppDbContext(options);
    }
}
