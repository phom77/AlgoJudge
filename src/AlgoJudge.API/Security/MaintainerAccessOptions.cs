namespace AlgoJudge.API.Security;

public sealed class MaintainerAccessOptions
{
    public const string SectionName = "MaintainerAccess";
    public IReadOnlyList<string> UserIds { get; init; } = [];

    public IReadOnlySet<Guid> ParseUserIds()
    {
        var result = new HashSet<Guid>();
        foreach (var value in UserIds)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
                throw new InvalidOperationException("MaintainerAccess:UserIds must contain valid non-empty UUIDs.");
            result.Add(id);
        }
        return result;
    }
}
