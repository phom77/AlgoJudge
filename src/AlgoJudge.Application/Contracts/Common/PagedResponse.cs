namespace AlgoJudge.Application.Contracts.Common
{
    public sealed class PagedResponse<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();
        public int TotalCount { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalPages => PageSize == 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
