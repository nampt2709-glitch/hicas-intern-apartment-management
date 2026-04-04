using ApartmentManagement.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace ApartmentManagement.UnitTests.RateLimiting;

public sealed class QuotaRateLimiterTests
{
    [Fact]
    public void TryConsume_Allows_Exactly_Limit_Then_Blocks()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new QuotaRateLimiter(cache);
        const string key = "test:quota:1";

        for (var i = 0; i < 5; i++)
        {
            sut.TryConsume(key, 5, TimeSpan.FromHours(1), out var rem, out _).Should().BeTrue();
            rem.Should().Be(5 - i - 1);
        }

        sut.TryConsume(key, 5, TimeSpan.FromHours(1), out var remaining, out _).Should().BeFalse();
        remaining.Should().Be(0);
    }

    [Fact]
    public void IsExceeded_When_No_State_Returns_False()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new QuotaRateLimiter(cache);
        sut.IsExceeded("fresh", 3, TimeSpan.FromMinutes(1), out var rem, out _).Should().BeFalse();
        rem.Should().Be(3);
    }
}
