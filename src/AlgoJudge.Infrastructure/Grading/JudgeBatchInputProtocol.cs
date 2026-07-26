using System.Globalization;
using System.Text;

namespace AlgoJudge.Infrastructure.Grading;

internal static class JudgeBatchInputProtocol
{
    private const string Name = "ALGOJUDGE_BATCH_INPUT_V1";

    public static string Serialize(IReadOnlyList<string> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            throw new ArgumentException("A batch requires at least one testcase.", nameof(inputs));

        var builder = new StringBuilder();
        builder.Append(Name)
            .Append('\n')
            .Append("case_count=")
            .Append(inputs.Count.ToString(CultureInfo.InvariantCulture))
            .Append("\n\n");
        foreach (var input in inputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            builder.Append("input_length=")
                .Append(Encoding.UTF8.GetByteCount(input).ToString(CultureInfo.InvariantCulture))
                .Append("\n\n")
                .Append(input);
        }

        return builder.ToString();
    }
}
