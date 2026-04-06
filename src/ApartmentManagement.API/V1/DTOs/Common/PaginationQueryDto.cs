// DTO tham số truy vấn phân trang, tìm kiếm và sắp xếp danh sách.
namespace ApartmentManagement.API.V1.DTOs.Common;

public class PaginationQueryDto
{
    // Số trang (bắt đầu từ 1).
    public int PageNumber { get; set; } = 1;
    // Số bản ghi mỗi trang.
    public int PageSize { get; set; } = 20;
    // Chuỗi tìm kiếm toàn cục (tùy backend áp dụng).
    public string? Search { get; set; }
    // Tên trường hoặc cột để sắp xếp.
    public string? SortBy { get; set; }
    // True = giảm dần, false = tăng dần.
    public bool Descending { get; set; } = false;
    // Có bao gồm bản ghi đã xóa mềm hay không.
    public bool IncludeDeleted { get; set; } = false;
}
