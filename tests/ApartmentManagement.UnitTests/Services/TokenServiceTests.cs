using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Settings;
using ApartmentManagement.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ApartmentManagement.UnitTests.Services;

public sealed class TokenServiceTests
{
    private static TokenService CreateSut()
    {
        var jwt = new JwtSettings
        {
            Key = new string('k', 32),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };
        return new TokenService(Options.Create(jwt));
    }

    [Fact]
    public void HashToken_SameInput_Produces_SameHex()
    {
        var sut = CreateSut();
        var a = sut.HashToken("refresh-token-sample");
        var b = sut.HashToken("refresh-token-sample");
        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }

    [Fact]
    public async Task CreateTokensAsync_Returns_Access_And_Refresh()
    {
        var sut = CreateSut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "u@test.local",
            UserName = "u@test.local",
            FullName = "User"
        };
        var pair = await sut.CreateTokensAsync(user, new[] { "User" }, CancellationToken.None);
        pair.AccessToken.Should().NotBeNullOrWhiteSpace();
        pair.RefreshToken.Should().NotBeNullOrWhiteSpace();
        pair.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        pair.RefreshTokenExpiresAt.Should().BeAfter(pair.AccessTokenExpiresAt);
    }
}
