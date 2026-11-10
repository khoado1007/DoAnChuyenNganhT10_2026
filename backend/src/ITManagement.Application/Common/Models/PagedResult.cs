namespace ITManagement.Application.Common.Models;

/// <summary>
/// Phân trang cho các danh sách dữ liệu lớn.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Tạo kết quả phân trang cùng thông tin tổng số bản ghi.</summary>
    public static PagedResult<T> Create(IEnumerable<T> items, int page, int pageSize, int totalCount) =>
        new()
        {
            Items = items.ToList().AsReadOnly(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
}
