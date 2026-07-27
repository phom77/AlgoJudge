using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlgoJudge.Infrastructure.Security;

public sealed class AdminBootstrapper : IAdminBootstrapper
{
    private readonly AppDbContext context;

    public AdminBootstrapper(AppDbContext context) => this.context = context;

    public async Task PromoteConfiguredUsersAsync(
        IReadOnlySet<string> normalizedEmails,
        CancellationToken cancellationToken = default)
    {
        if (normalizedEmails.Count == 0)
        {
            return;
        }

        var emails = normalizedEmails.ToArray();
        var users = await context.Users
            .Where(user => emails.Contains(user.Email.ToUpper()))
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var user in users.Where(user => user.Role != UserRole.Admin))
        {
            user.Role = UserRole.Admin;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
