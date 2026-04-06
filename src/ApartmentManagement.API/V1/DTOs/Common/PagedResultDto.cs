// DTO kết quả phân trang: danh sách phần tử và siêu dữ liệu trang.
namespace ApartmentManagement.API.V1.DTOs.Common;

public class PagedResultDto<T>
{
    // Các phần tử của trang hiện tại.
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    // Số trang hiện tại.
    public int PageNumber { get; set; }
    // Kích thước trang.
    public int PageSize { get; set; }
    // Tổng số bản ghi khớp điều kiện.
    public int TotalCount { get; set; }
    // Tổng số trang (làm tròn lên).
    public int TotalPages { get; set; }
}
