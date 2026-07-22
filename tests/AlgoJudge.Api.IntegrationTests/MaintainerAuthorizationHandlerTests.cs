using System.Security.Claims;
using AlgoJudge.API.Security;
using Microsoft.AspNetCore.Authorization;

namespace AlgoJudge.Api.IntegrationTests;

public sealed class MaintainerAuthorizationHandlerTests
{
    [Fact]
    public async Task OnlyConfiguredAuthenticatedUserSatisfiesMaintainerPolicy()
    {
        var maintainerId = Guid.NewGuid();
        var handler = new MaintainerAuthorizationHandler(new MaintainerAccessOptions
        {
            UserIds = [maintainerId.ToString()]
        });
        var requirement = new MaintainerRequirement();
        var allowed = new AuthorizationHandlerContext([requirement], Principal(maintainerId), null);
        var denied = new AuthorizationHandlerContext([requirement], Principal(Guid.NewGuid()), null);

        await handler.HandleAsync(allowed);
        await handler.HandleAsync(denied);

        Assert.True(allowed.HasSucceeded);
        Assert.False(denied.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(Guid id) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
}
