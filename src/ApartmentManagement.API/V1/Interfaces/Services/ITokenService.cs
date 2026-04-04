using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface ITokenService
{
    Task<TokenPairDto> CreateTokensAsync(ApplicationUser user, IList<string> roles, CancellationToken cancellationToken = default);
    Task<string> GenerateAccessTokenAsync(ApplicationUser user, IList<string> roles, Guid jti, CancellationToken cancellationToken = default);
    string HashToken(string token);
    string GenerateRefreshToken();
}
