using AlgoJudge.API.Controllers;
using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.Api.IntegrationTests;

public class PublicApiScopeTests
{
    [Fact]
    public void LegacyLeaderboardAndTestCaseControllersAreNotExposed()
    {
        var controllerNames = typeof(ProblemsController).Assembly
            .GetExportedTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        Assert.DoesNotContain("LeaderboardController", controllerNames);
        Assert.DoesNotContain("TestCasesController", controllerNames);
    }

    [Fact]
    public void ProblemControllerIsReadOnly()
    {
        var actionMethods = typeof(ProblemsController)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(ProblemsController))
            .ToList();

        Assert.DoesNotContain(actionMethods,
            method => method.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);
        Assert.DoesNotContain(actionMethods,
            method => method.GetCustomAttributes(typeof(HttpPutAttribute), true).Length > 0);
        Assert.DoesNotContain(actionMethods,
            method => method.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0);
    }

    [Fact]
    public void ProblemDetailRouteUsesSlug()
    {
        var action = typeof(ProblemsController).GetMethod(nameof(ProblemsController.GetBySlug));
        var route = Assert.Single(action!.GetCustomAttributes(typeof(HttpGetAttribute), true))
            as HttpGetAttribute;

        Assert.Equal("{slug}", route!.Template);
        Assert.Equal(typeof(string), action.GetParameters().Single().ParameterType);
    }

    [Fact]
    public void PublicProblemContractDoesNotExposeJudgeTestCases()
    {
        Assert.DoesNotContain(
            typeof(ProblemDetailResponse).GetProperties(),
            property => property.Name.Contains("TestCase", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicResponseContractsExcludeOperationalAndSensitiveFields()
    {
        var forbiddenNames = new[]
        {
            "JudgeTestCase",
            "Hidden",
            "SourceCode",
            "WorkerId",
            "ClaimToken",
            "LeaseExpiresAt",
            "AttemptCount"
        };
        var responseProperties = typeof(IAuthService).Assembly
            .GetExportedTypes()
            .Where(type =>
                type.Namespace?.Contains(".Contracts.", StringComparison.Ordinal) == true &&
                type.Namespace?.Contains(".Contracts.Admin", StringComparison.Ordinal) != true &&
                type.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties().Select(property =>
                $"{type.Name}.{property.Name}"))
            .ToArray();

        Assert.DoesNotContain(responseProperties, property =>
            forbiddenNames.Any(forbidden =>
                property.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void InternalAuthoringResponsesExcludeCandidatePayloadsAndFencingData()
    {
        var properties = typeof(AlgoJudge.Application.Contracts.Admin.GeneratedSuiteReviewResponse).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.Contains(".Contracts.Admin", StringComparison.Ordinal) == true &&
                type.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties().Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain(properties, name => name.Contains("Input", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("ExpectedOutput", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("ClaimToken", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("WorkerId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Lease", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EverySubmissionEndpointRequiresAuthentication()
    {
        Assert.NotEmpty(typeof(SubmissionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.DoesNotContain(
            typeof(SubmissionsController).GetMethods()
                .Where(method => method.DeclaringType == typeof(SubmissionsController)),
            method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Length > 0);
    }
}
