using AlgoJudge.API.Controllers;
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
}
