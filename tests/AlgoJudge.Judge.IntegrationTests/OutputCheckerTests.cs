using AlgoJudge.Domain.Enums;
using AlgoJudge.Domain.Execution;
using AlgoJudge.Infrastructure.Grading;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class OutputCheckerTests
{
    private readonly OutputChecker _checker = new();

    [Fact]
    public void TokenExactIgnoresUnicodeWhitespaceButNotTokenBoundaries()
    {
        Assert.True(_checker.IsMatch(
            OutputCheckerConfiguration.TokenExact,
            "alpha beta\n",
            "\u00a0alpha\t beta \r\n"));
        Assert.False(_checker.IsMatch(
            OutputCheckerConfiguration.TokenExact,
            "12 3",
            "123"));
    }

    [Fact]
    public void JsonExactComparesStructuredJsonAndRejectsMalformedOutput()
    {
        Assert.True(_checker.IsMatch(
            OutputCheckerConfiguration.JsonExact,
            "{\"answer\":[1,2],\"ok\":true}",
            " { \"ok\" : true, \"answer\" : [ 1, 2 ] } \n"));
        Assert.False(_checker.IsMatch(
            OutputCheckerConfiguration.JsonExact,
            "[1,2]",
            "[1,2"));
    }

    [Fact]
    public void FloatingPointUsesAbsoluteOrRelativeToleranceAndRejectsNonFiniteTokens()
    {
        var configuration = new OutputCheckerConfiguration(
            OutputCheckerKind.FloatingPoint,
            AbsoluteTolerance: 0.001,
            RelativeTolerance: 0.01);

        Assert.True(_checker.IsMatch(configuration, "100 0", "100.5 0.0005"));
        Assert.False(_checker.IsMatch(configuration, "100 0", "102 0.002"));
        Assert.False(_checker.IsMatch(configuration, "1", "NaN"));
    }

    [Fact]
    public void InvalidCheckerConfigurationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => _checker.IsMatch(
            new OutputCheckerConfiguration(OutputCheckerKind.JsonExact, AbsoluteTolerance: 0.1),
            "1",
            "1"));
    }
}
