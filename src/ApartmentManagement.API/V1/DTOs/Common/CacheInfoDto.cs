// DTO mô tả trạng thái cache (backend, hit/miss) cho phản hồi API.
namespace ApartmentManagement.API.V1.DTOs.Common;

// Loại backend lưu cache được sử dụng.
public enum CacheBackendType
{
    // Không dùng cache.
    None = 0,
    // Redis.
    Redis = 1,
    // Bộ nhớ trong tiến trình.
    InMemory = 2
}

public sealed class CacheInfoDto
{
    // Loại backend cache.
    public CacheBackendType Backend { get; set; } = CacheBackendType.None;
    // True nếu đọc được từ cache (hit).
    public bool Hit { get; set; }
    // Chuỗi trạng thái ngắn (none/hit/miss).
    public string Status { get; set; } = "none";

    // Không dùng cache.
    public static CacheInfoDto None => new() { Backend = CacheBackendType.None, Hit = false, Status = "none" };
    // Redis trúng cache.
    public static CacheInfoDto RedisHit => new() { Backend = CacheBackendType.Redis, Hit = true, Status = "hit" };
    // Redis trượt cache.
    public static CacheInfoDto RedisMiss => new() { Backend = CacheBackendType.Redis, Hit = false, Status = "miss" };
    // In-memory trúng cache.
    public static CacheInfoDto InMemoryHit => new() { Backend = CacheBackendType.InMemory, Hit = true, Status = "hit" };
    // In-memory trượt cache.
    public static CacheInfoDto InMemoryMiss => new() { Backend = CacheBackendType.InMemory, Hit = false, Status = "miss" };
}
