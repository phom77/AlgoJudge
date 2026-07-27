namespace AlgoJudge.Application.Interfaces;

public interface IAdminBootstrapper
{
    Task PromoteConfiguredUsersAsync(
        IReadOnlySet<string> normalizedEmails,
        CancellationToken cancellationToken = default);
}
