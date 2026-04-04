namespace ApartmentManagement.Utilities;

public static class UploadPathSecurity
{
    public static string NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        return Path.GetFullPath(path);
    }

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
