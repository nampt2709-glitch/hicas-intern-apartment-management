using ApartmentManagement.API.V1.DTOs.Common;

namespace ApartmentManagement.Utilities;

// Mở rộng: gói danh sách + metadata phân trang (tổng bản ghi, tổng trang).
public static class QueryableExtensions
{
    public static PagedResultDto<T> ToPagedResult<T>(this IEnumerable<T> items, int pageNumber, int pageSize, int totalCount)
    {
        // tính TotalPages = ceil(totalCount / pageSize); pageSize=0 → 0 trang.
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResultDto<T>
        {
            Items = items.ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
