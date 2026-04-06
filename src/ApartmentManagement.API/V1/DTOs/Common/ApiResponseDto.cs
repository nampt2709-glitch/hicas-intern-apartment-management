// DTO bọc phản hồi API thống nhất (thành công, thông báo, dữ liệu).
namespace ApartmentManagement.API.V1.DTOs.Common;

public class ApiResponseDto<T>
{
    // True nếu thao tác thành công.
    public bool Success { get; set; } = true;
    // Thông báo cho người dùng hoặc client.
    public string Message { get; set; } = string.Empty;
    // Payload kết quả (có thể null khi lỗi hoặc không có dữ liệu).
    public T? Data { get; set; }
}
