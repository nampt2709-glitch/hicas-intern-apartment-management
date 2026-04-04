using DotNetEnv;

namespace ApartmentManagement.Utilities;

public static class DotEnvLoader
{
    /// <summary>Loads the first <c>.env</c> found from <paramref name="startDirectory"/> upward, then current directory chain.</summary>
    public static void TryLoad(string? startDirectory = null)
    {
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryFile(string? dir)
            {
                if (string.IsNullOrWhiteSpace(dir))
                    return;
                var full = Path.GetFullPath(dir);
                if (!seen.Add(full))
                    return;
                var envPath = Path.Combine(full, ".env");
                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    return;
                }
            }

            TryFile(startDirectory);

            TryFile(Directory.GetCurrentDirectory());
            var walk = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(walk))
            {
                TryFile(walk);
                var parent = Directory.GetParent(walk);
                walk = parent?.FullName ?? string.Empty;
            }

            if (File.Exists(".env"))
                Env.Load(".env");
        }
        catch
        {
            /* best-effort */
        }
    }
}
