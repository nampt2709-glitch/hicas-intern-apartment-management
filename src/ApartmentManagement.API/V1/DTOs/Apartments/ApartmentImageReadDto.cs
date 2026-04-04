namespace ApartmentManagement.API.V1.DTOs.Apartments;

public class ApartmentImageReadDto
{
    public Guid Id { get; set; }
    public Guid ApartmentId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
