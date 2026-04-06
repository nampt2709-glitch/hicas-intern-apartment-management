// DTO đọc một ảnh thuộc album của căn hộ (đường dẫn file, thứ tự).
namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentImageReadDto
{
    // Khóa chính ảnh.
    public Guid Id { get; set; }
    // Căn hộ sở hữu ảnh.
    public Guid ApartmentId { get; set; }
    // Đường dẫn lưu trữ file.
    public string FilePath { get; set; } = string.Empty;
    // Tên file gốc khi upload.
    public string OriginalFileName { get; set; } = string.Empty;
    // Loại MIME.
    public string MimeType { get; set; } = string.Empty;
    // Thứ tự hiển thị trong gallery.
    public int SortOrder { get; set; }
}
