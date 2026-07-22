using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AlgoJudge.API.Security;

public sealed class MaintainerAuthorizationHandler : AuthorizationHandler<MaintainerRequirement>
{
    private readonly IReadOnlySet<Guid> _maintainerIds;
    public MaintainerAuthorizationHandler(MaintainerAccessOptions options) =>
        _maintainerIds = options.ParseUserIds();

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MaintainerRequirement requirement)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (Guid.TryParse(value, out var id) && _maintainerIds.Contains(id)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
