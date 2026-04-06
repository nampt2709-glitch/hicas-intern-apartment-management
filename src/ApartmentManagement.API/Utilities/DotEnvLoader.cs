using DotNetEnv;

namespace ApartmentManagement.Utilities;

// Nạp file .env (best-effort) để biến môi trường có sẵn trước khi Configuration đọc.
public static class DotEnvLoader
{
    // Tìm file .env từ thư mục start (nếu có) đi lên parent, rồi từ thư mục hiện tại lên root.
    public static void TryLoad(string? startDirectory = null)
    {
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Hàm cục bộ: thử một thư mục, tránh trùng lặp đường dẫn (case-insensitive).
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

            // Duyệt từ thư mục hiện tại lên tới root để tìm .env.
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
            /* Bỏ qua lỗi — môi trường vẫn có thể được set từ Docker/OS. */
        }
    }
}
