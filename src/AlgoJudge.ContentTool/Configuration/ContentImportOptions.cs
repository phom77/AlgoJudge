namespace AlgoJudge.ContentTool.Configuration;

public sealed class ContentImportOptions
{
    public const string SectionName = "ContentImport";

    public long MaxArchiveBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxTotalUncompressedBytes { get; set; } = 100 * 1024 * 1024;
    public long MaxEntryBytes { get; set; } = 8 * 1024 * 1024;
    public int MaxFileCount { get; set; } = 1_100;
    public int MaxSampleCount { get; set; } = 20;
    public int MaxJudgeTestCaseCount { get; set; } = 500;
    public int MinTimeLimitMs { get; set; } = 100;
    public int MaxTimeLimitMs { get; set; } = 10_000;
    public int MinMemoryLimitKb { get; set; } = 16 * 1024;
    public int MaxMemoryLimitKb { get; set; } = 1024 * 1024;

    public void Validate()
    {
        if (MaxArchiveBytes <= 0 ||
            MaxTotalUncompressedBytes <= 0 ||
            MaxEntryBytes <= 0)
        {
            throw new InvalidOperationException("Content size limits must be positive.");
        }

        if (MaxArchiveBytes > MaxTotalUncompressedBytes)
        {
            throw new InvalidOperationException(
                "MaxArchiveBytes cannot exceed MaxTotalUncompressedBytes.");
        }

        if (MaxEntryBytes > MaxTotalUncompressedBytes)
        {
            throw new InvalidOperationException(
                "MaxEntryBytes cannot exceed MaxTotalUncompressedBytes.");
        }

        if (MaxFileCount < 5 || MaxSampleCount < 1 || MaxJudgeTestCaseCount < 1)
        {
            throw new InvalidOperationException("Content count limits are invalid.");
        }

        if (MinTimeLimitMs <= 0 || MaxTimeLimitMs < MinTimeLimitMs)
        {
            throw new InvalidOperationException("Time-limit bounds are invalid.");
        }

        if (MinMemoryLimitKb <= 0 || MaxMemoryLimitKb < MinMemoryLimitKb)
        {
            throw new InvalidOperationException("Memory-limit bounds are invalid.");
        }
    }
}
