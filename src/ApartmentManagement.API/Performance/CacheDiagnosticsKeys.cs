namespace ApartmentManagement.Performance;

// Khóa lưu CacheInfoDto trong HttpContext.Items (HybridCacheService ghi, header đọc).
public static class CacheDiagnosticsKeys
{
    public const string HttpContextItemKey = "__APARTMENT_CACHE_INFO__";
}
