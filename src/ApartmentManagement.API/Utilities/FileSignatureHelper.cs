namespace ApartmentManagement.Utilities;

public enum DetectedImageFormat
{
    None,
    Jpeg,
    Png,
    Webp
}

public static class FileSignatureHelper
{
    public static DetectedImageFormat DetectFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return DetectedImageFormat.Png;

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return DetectedImageFormat.Jpeg;

        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return DetectedImageFormat.Webp;

        return DetectedImageFormat.None;
    }

    public static bool ExtensionMatchesFormat(string extensionLower, DetectedImageFormat format)
    {
        return format switch
        {
            DetectedImageFormat.Jpeg => extensionLower is ".jpg" or ".jpeg",
            DetectedImageFormat.Png => extensionLower == ".png",
            DetectedImageFormat.Webp => extensionLower == ".webp",
            _ => false
        };
    }

    public static string ToCanonicalMimeType(DetectedImageFormat format) => format switch
    {
        DetectedImageFormat.Jpeg => "image/jpeg",
        DetectedImageFormat.Png => "image/png",
        DetectedImageFormat.Webp => "image/webp",
        _ => string.Empty
    };
}
