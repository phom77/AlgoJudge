using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Domain.Execution;

public sealed record OutputCheckerConfiguration(
    OutputCheckerKind Kind,
    double? AbsoluteTolerance = null,
    double? RelativeTolerance = null)
{
    public static OutputCheckerConfiguration TokenExact { get; } =
        new(OutputCheckerKind.TokenExact);

    public static OutputCheckerConfiguration JsonExact { get; } =
        new(OutputCheckerKind.JsonExact);

    public void Validate()
    {
        if (!Enum.IsDefined(Kind))
            throw new ArgumentOutOfRangeException(nameof(Kind), "The output checker kind is invalid.");

        if (Kind is OutputCheckerKind.TokenExact or OutputCheckerKind.JsonExact)
        {
            if (AbsoluteTolerance is not null || RelativeTolerance is not null)
                throw new ArgumentException("Only the floating-point checker accepts tolerances.");
            return;
        }

        if (!IsValidTolerance(AbsoluteTolerance) || !IsValidTolerance(RelativeTolerance) ||
            (AbsoluteTolerance.GetValueOrDefault() == 0 && RelativeTolerance.GetValueOrDefault() == 0))
        {
            throw new ArgumentException(
                "The floating-point checker requires at least one finite positive tolerance.");
        }
    }

    private static bool IsValidTolerance(double? value) =>
        value is { } tolerance && double.IsFinite(tolerance) && tolerance >= 0;
}
