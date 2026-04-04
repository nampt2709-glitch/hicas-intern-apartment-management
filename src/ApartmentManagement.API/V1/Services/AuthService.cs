using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities.Security;
using Microsoft.AspNetCore.Identity;

namespace ApartmentManagement.API.V1.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IReferenceEntityLookup _refs;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly ITokenRevocationService _revocationService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IReferenceEntityLookup referenceEntityLookup,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ITokenRevocationService revocationService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _refs = referenceEntityLookup;
        _refreshTokens = refreshTokenRepository;
        _tokenService = tokenService;
        _revocationService = revocationService;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureRolesExistAsync(cancellationToken);

        var email = dto.Email.Trim();
        if (!await _refs.IsEmailAvailableForAnotherUserAsync(email, null, cancellationToken))
            throw new InvalidOperationException("This email is already registered.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "registration"
        };

        var create = await _userManager.CreateAsync(user, dto.Password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        var addRole = await _userManager.AddToRoleAsync(user, "User");
        if (!addRole.Succeeded)
            throw new InvalidOperationException(string.Join("; ", addRole.Errors.Select(e => e.Description)));

        return await IssueAuthResultAsync(user, cancellationToken);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(dto.Email)
                   ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return await IssueAuthResultAsync(user, cancellationToken);
    }

    private async Task EnsureRolesExistAsync(CancellationToken cancellationToken)
    {
        foreach (var name in new[] { "Admin", "User" })
        {
            if (!await _roleManager.RoleExistsAsync(name))
            {
                var r = await _roleManager.CreateAsync(new IdentityRole<Guid>(name));
                if (!r.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", r.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task<AuthResultDto> IssueAuthResultAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var tokens = await _tokenService.CreateTokensAsync(user, roles, cancellationToken);

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(tokens.RefreshToken),
            JwtId = Guid.NewGuid().ToString("N"),
            ExpiresAt = tokens.RefreshTokenExpiresAt,
            CreatedAt = DateTime.UtcNow
        };
        await _refreshTokens.AddAsync(refresh, cancellationToken);
        await _refreshTokens.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList(),
            Tokens = tokens
        };
    }

    public async Task<TokenPairDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hashed = _tokenService.HashToken(dto.RefreshToken);
        var existing = await _refreshTokens.GetActiveByHashWithUserAsync(hashed, cancellationToken)
                       ?? throw new UnauthorizedAccessException("Refresh token is invalid.");

        var utc = DateTime.UtcNow;
        existing.RevokedAt = utc;
        existing.IsDeleted = true;
        existing.DeletedAt = utc;
        existing.UpdatedAt = utc;

        var roles = await _userManager.GetRolesAsync(existing.User);
        var tokens = await _tokenService.CreateTokensAsync(existing.User, roles, cancellationToken);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = _tokenService.HashToken(tokens.RefreshToken),
            JwtId = Guid.NewGuid().ToString("N"),
            ExpiresAt = tokens.RefreshTokenExpiresAt,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _refreshTokens.SaveChangesAsync(cancellationToken);
        return tokens;
    }

    public async Task<LogoutResultDto> LogoutAsync(
        Guid currentUserId,
        string? accessToken,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RefreshToken? refreshRow = null;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = _tokenService.HashToken(refreshToken);
            refreshRow = await _refreshTokens.GetByHashAsync(hash, cancellationToken);
            if (refreshRow is not null && refreshRow.UserId != currentUserId)
                throw new UnauthorizedAccessException("Refresh token does not belong to the current user.");
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
            await _revocationService.RevokeAccessTokenAsync(accessToken, cancellationToken);

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _revocationService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
            if (refreshRow is not null)
            {
                var utc = DateTime.UtcNow;
                refreshRow.RevokedAt = utc;
                refreshRow.IsDeleted = true;
                refreshRow.DeletedAt = utc;
                refreshRow.UpdatedAt = utc;
                await _refreshTokens.SaveChangesAsync(cancellationToken);
            }
        }

        return new LogoutResultDto { LoggedOut = true };
    }

    public async Task<CurrentUserDto> MeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new KeyNotFoundException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarPath = user.AvatarPath,
            Roles = roles.ToList()
        };
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return string.Empty;

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return;

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }
}
