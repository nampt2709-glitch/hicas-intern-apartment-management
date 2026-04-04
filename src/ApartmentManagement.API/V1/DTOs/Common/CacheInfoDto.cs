namespace ApartmentManagement.API.V1.DTOs.Common;

public enum CacheBackendType
{
    None = 0,
    Redis = 1,
    InMemory = 2
}

public sealed class CacheInfoDto
{
    public CacheBackendType Backend { get; set; } = CacheBackendType.None;
    public bool Hit { get; set; }
    public string Status { get; set; } = "none";

    public static CacheInfoDto None => new() { Backend = CacheBackendType.None, Hit = false, Status = "none" };
    public static CacheInfoDto RedisHit => new() { Backend = CacheBackendType.Redis, Hit = true, Status = "hit" };
    public static CacheInfoDto RedisMiss => new() { Backend = CacheBackendType.Redis, Hit = false, Status = "miss" };
    public static CacheInfoDto InMemoryHit => new() { Backend = CacheBackendType.InMemory, Hit = true, Status = "hit" };
    public static CacheInfoDto InMemoryMiss => new() { Backend = CacheBackendType.InMemory, Hit = false, Status = "miss" };
}
