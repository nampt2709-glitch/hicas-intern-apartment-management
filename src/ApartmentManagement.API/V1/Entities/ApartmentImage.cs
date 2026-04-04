namespace ApartmentManagement.API.V1.Entities;

public class ApartmentImage : BaseEntity
{
    public Guid ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
