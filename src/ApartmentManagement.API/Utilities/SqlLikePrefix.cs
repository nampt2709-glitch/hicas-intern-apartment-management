namespace ApartmentManagement.Utilities;

public static class SqlLikePrefix
{
    public static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return input.Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal);
    }

    public static string ForStartsWith(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return "%";
        return Escape(prefix) + "%";
    }
}
