// DTO kết quả sau khi tải file lên (đường dẫn lưu trữ và siêu dữ liệu).
namespace ApartmentManagement.API.V1.DTOs.Uploads;

public class UploadResultDto
{
    // Đường dẫn file trên máy chủ hoặc storage.
    public string FilePath { get; set; } = string.Empty;
    // Tên file gốc hoặc tên hiển thị.
    public string FileName { get; set; } = string.Empty;
    // Loại MIME (ví dụ image/jpeg).
    public string MimeType { get; set; } = string.Empty;
    // Kích thước file tính bằng byte.
    public long Size { get; set; }
}
