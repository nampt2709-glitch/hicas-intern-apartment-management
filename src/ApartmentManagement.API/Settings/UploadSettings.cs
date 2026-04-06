namespace ApartmentManagement.Settings;

// Cấu trúc vật lý dưới <see cref="BasePath"/>: <c>avatars/{userId}/</c>, <c>apartmentImages/{apartmentId}/</c>. <see cref="StagingPath"/> phải nằm trong <see cref="BasePath"/> (kiểm tra path traversal).
public sealed class UploadSettings
{
    public string BasePath { get; set; } = "/app/uploads";
    public string StagingPath { get; set; } = "/app/uploads/_staging";
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxFilesPerRequest { get; set; } = 5;
    public int MaxImageWidth { get; set; } = 8192;
    public int MaxImageHeight { get; set; } = 8192;
    public bool EnableDuplicateDetection { get; set; } = true;
    public bool EnableMalwareScan { get; set; } = true;
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
    public string[] AllowedMimeTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
}
