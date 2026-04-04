using ApartmentManagement.API.V1.DTOs.Common;

namespace ApartmentManagement.Utilities;

public static class QueryableExtensions
{
    public static PagedResultDto<T> ToPagedResult<T>(this IEnumerable<T> items, int pageNumber, int pageSize, int totalCount)
    {
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
