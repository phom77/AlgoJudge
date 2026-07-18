using AlgoJudge.Application.ContentGeneration;
using System.Text.Json;

namespace AlgoJudge.ContentTool.Generation;

internal static class ProblemGenerationMetadataReader
{
    public static async Task<ReferenceSolutionLimits> ReadLimitsAsync(
        string problemDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(problemDirectory, "problem.json");
        if (!File.Exists(path))
            throw new TestGenerationException("problem.json is required for test generation.");

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("timeLimitMs", out var timeProperty) ||
                !timeProperty.TryGetInt32(out var timeLimitMs) ||
                !root.TryGetProperty("memoryLimitKb", out var memoryProperty) ||
                !memoryProperty.TryGetInt32(out var memoryLimitKb) ||
                timeLimitMs <= 0 ||
                memoryLimitKb < 16 * 1024)
            {
                throw new TestGenerationException(
                    "problem.json must contain valid timeLimitMs and memoryLimitKb values.");
            }

            return new ReferenceSolutionLimits(timeLimitMs, memoryLimitKb);
        }
        catch (JsonException)
        {
            throw new TestGenerationException("problem.json is invalid.");
        }
    }
}
