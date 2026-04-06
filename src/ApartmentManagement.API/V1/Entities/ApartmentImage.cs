namespace ApartmentManagement.API.V1.Entities;

// Ảnh minh hoạ thuộc một căn hộ (gallery), có thứ tự sắp xếp.
public class ApartmentImage : BaseEntity
{
    public Guid ApartmentId { get; set; }
    // Điều hướng: căn hộ chứa ảnh (một căn — nhiều ảnh).
    public Apartment Apartment { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
