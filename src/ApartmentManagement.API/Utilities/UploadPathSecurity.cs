namespace ApartmentManagement.Utilities;

// Chống path traversal: chuẩn hóa đường dẫn và đảm bảo nằm dưới thư mục gốc upload.
public static class UploadPathSecurity
{
    // resolve full path; throw nếu rỗng.
    public static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        return Path.GetFullPath(path);
    }

    // candidate phải bắt đầu bằng root (hoặc bằng chính root) sau khi full path.
    public static void AssertPathUnderRoot(string rootFullPath, string candidateFullPath)
    {
        var root = Path.GetFullPath(rootFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(candidateFullPath);
        var prefix = root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved path escapes the allowed upload root.");
    }
}
