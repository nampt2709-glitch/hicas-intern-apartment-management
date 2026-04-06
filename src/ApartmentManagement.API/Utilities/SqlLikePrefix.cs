namespace ApartmentManagement.Utilities;

// Hỗ trợ xây mẫu LIKE an toàn: escape ký tự đặc biệt và prefix "bắt đầu bằng".
public static class SqlLikePrefix
{
    // escape [, %, _] cho SQL LIKE.
    public static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return input.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal);
    }

    // prefix rỗng → "%" (không lọc); còn lại Escape + "%" ở cuối.
    public static string ForStartsWith(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return "%";
        return Escape(prefix) + "%";
    }
}
