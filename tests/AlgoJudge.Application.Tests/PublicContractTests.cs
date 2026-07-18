using AlgoJudge.Application.Contracts.Auth;
using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Application.Contracts.Runs;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Services;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.Tests;

public class PublicContractTests
{
    [Fact]
    public void ApplicationDoesNotExposeLegacyDtoContracts()
    {
        var legacyContracts = typeof(IAuthService).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.Contains(".DTOs", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(legacyContracts);
        Assert.Null(typeof(AuthResponse).GetProperty("Token"));
        Assert.Null(typeof(AuthResponse).GetProperty("AccessToken"));
        Assert.Null(typeof(AuthResponse).GetProperty("RefreshToken"));
        Assert.Null(typeof(AuthResponse).GetProperty("TokenType"));
        Assert.Null(typeof(SubmissionResponse).GetProperty("UserId"));
        Assert.Null(typeof(RunResponse).GetProperty("UserId"));
        Assert.Null(typeof(RunResponse).GetProperty("SourceCode"));
        Assert.Null(typeof(RunResponse).GetProperty("Input"));
    }

    [Fact]
    public async Task SubmissionAcceptsOnlyCpp17()
    {
        var service = CreateSubmissionService();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.SubmitCodeAsync(
                new CreateSubmissionRequest
                {
                    ProblemId = 1,
                    Language = "cpp",
                    SourceCode = "int main() { return 0; }"
                },
                Guid.NewGuid()));

        Assert.Contains("cpp17", exception.Message);
    }

    [Fact]
    public async Task SubmissionLimitsSourceCodeByUtf8ByteCount()
    {
        var service = CreateSubmissionService();
        var sourceCode = new string('é',
            (SubmissionContractLimits.MaxSourceCodeBytes / 2) + 1);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.SubmitCodeAsync(
                new CreateSubmissionRequest
                {
                    ProblemId = 1,
                    Language = "cpp17",
                    SourceCode = sourceCode
                },
                Guid.NewGuid()));

        Assert.Contains("UTF-8 bytes", exception.Message);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task SubmissionHistoryRejectsInvalidPagination(
        int pageNumber,
        int pageSize)
    {
        var service = CreateSubmissionService();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.GetHistoryAsync(
                Guid.NewGuid(),
                new SubmissionHistoryQuery
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize
                }));
    }

    [Fact]
    public async Task SubmissionHistoryRejectsUndefinedStatus()
    {
        var service = CreateSubmissionService();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.GetHistoryAsync(
                Guid.NewGuid(),
                new SubmissionHistoryQuery
                {
                    Status = (SubmissionStatus)999
                }));
    }

    private static SubmissionService CreateSubmissionService()
    {
        return new SubmissionService(null!, null!, null!, null!);
    }
}
