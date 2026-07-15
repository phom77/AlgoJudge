namespace AlgoJudge.ContentTool.Packages;

public sealed class PackageValidationException : Exception
{
    public PackageValidationException(IEnumerable<string> errors)
        : base("The problem package is invalid.")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyCollection<string> Errors { get; }
}
