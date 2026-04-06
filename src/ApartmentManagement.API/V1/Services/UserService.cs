using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Utilities;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Services;

// Dịch vụ quản lý người dùng: tạo/cập nhật/xóa tài khoản, phân quyền và ánh xạ sang DTO.
public sealed class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IReferenceEntityLookup _refs;
    private readonly IMapper _mapper;
    private readonly IUserRepository _repository;
    private readonly ApartmentDbContext _db;

    // Khởi tạo dịch vụ người dùng với Identity, repository và DbContext.
    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IReferenceEntityLookup referenceEntityLookup,
        IMapper mapper,
        IUserRepository repository,
        ApartmentDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _refs = referenceEntityLookup;
        _mapper = mapper;
        _repository = repository;
        _db = db;
    }

    // Tạo người dùng mới, đảm bảo email chưa dùng và gán các vai trò theo yêu cầu.
    public async Task<CurrentUserDto> CreateAsync(CreateUserRequestDto dto, CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim();
        if (!await _refs.IsEmailAvailableForAnotherUserAsync(email, null, cancellationToken))
            throw new InvalidOperationException("This email is already registered.");

        foreach (var name in new[] { "Admin", "User" })
        {
            if (!await _roleManager.RoleExistsAsync(name))
            {
                var created = await _roleManager.CreateAsync(new IdentityRole<Guid>(name));
                if (!created.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        foreach (var roleName in dto.Roles
                     .Select(r => r.Trim())
                     .Where(r => r.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var add = await _userManager.AddToRoleAsync(user, roleName);
            if (!add.Succeeded)
                throw new InvalidOperationException(string.Join("; ", add.Errors.Select(e => e.Description)));
        }

        return await MapUserAsync(user, cancellationToken);
    }

    // Lấy danh sách người dùng phân trang kèm vai trò.
    public async Task<PagedResultDto<CurrentUserDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default)
    {
        var baseQuery = _repository.Query(true, query.IncludeDeleted);
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery.OrderByDescending(x => x.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var roleMap = await _repository.GetRoleNamesByUserIdsAsync(items.Select(x => x.Id).ToList(), cancellationToken);

        var dtos = new List<CurrentUserDto>(items.Count);
        foreach (var item in items)
        {
            var dto = _mapper.Map<CurrentUserDto>(item);
            dto.Roles = roleMap.TryGetValue(item.Id, out var roles) ? roles : new List<string>();
            dtos.Add(dto);
        }

        return dtos.ToPagedResult(query.PageNumber, query.PageSize, total);
    }

    // Lấy thông tin người dùng theo Id.
    public async Task<CurrentUserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new KeyNotFoundException("User not found.");
        return await MapUserAsync(user, cancellationToken);
    }

    // Cập nhật hồ sơ người dùng (quản trị), đồng bộ email và tên đăng nhập nếu đổi email.
    public async Task<CurrentUserDto> UpdateAsync(Guid id, CurrentUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new KeyNotFoundException("User not found.");

        user.FullName = dto.FullName;
        user.AvatarPath = dto.AvatarPath;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.Equals(user.Email, dto.Email, StringComparison.Ordinal))
        {
            var emailResult = await _userManager.SetEmailAsync(user, dto.Email);
            if (!emailResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", emailResult.Errors.Select(e => e.Description)));
        }

        if (!string.Equals(user.UserName, dto.Email, StringComparison.Ordinal))
        {
            var nameResult = await _userManager.SetUserNameAsync(user, dto.Email);
            if (!nameResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", nameResult.Errors.Select(e => e.Description)));
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return await MapUserAsync(user, cancellationToken);
    }

    // Người dùng tự cập nhật hồ sơ; có thể đổi mật khẩu khi cung cấp mật khẩu hiện tại.
    public async Task<CurrentUserDto> UpdateMeAsync(Guid userId, UpdateMyProfileDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new KeyNotFoundException("User not found.");

        user.FullName = dto.FullName;
        if (dto.PhoneNumber is not null)
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        user.UpdatedAt = DateTime.UtcNow;

        var profileUpdate = await _userManager.UpdateAsync(user);
        if (!profileUpdate.Succeeded)
            throw new InvalidOperationException(string.Join("; ", profileUpdate.Errors.Select(e => e.Description)));

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                throw new InvalidOperationException("Current password is required to set a new password.");

            var pwdResult = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!pwdResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", pwdResult.Errors.Select(e => e.Description)));

            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        return await MapUserAsync(user, cancellationToken);
    }

    // Quản trị đặt lại mật khẩu cho người dùng bằng token reset của Identity.
    public async Task<AdminPasswordResetResultDto> AdminResetPasswordAsync(
        Guid userId,
        ResetUserPasswordDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
                   ?? throw new KeyNotFoundException("User not found.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return new AdminPasswordResetResultDto { Reset = true };
    }

    // Xóa mềm người dùng (đánh dấu IsDeleted).
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
                   ?? throw new KeyNotFoundException("User not found.");
        var utc = DateTime.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = utc;
        user.UpdatedAt = utc;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    // Xóa vĩnh viễn người dùng và dữ liệu liên quan trong hệ thống Identity.
    public async Task PurgeUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Set<ApplicationUser>().IgnoreQueryFilters()
            .AnyAsync(u => u.Id == id, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("User not found.");

        var identityResult = await UserAccountPurge.DeleteUserHardAsync(_userManager, _db, id, cancellationToken);
        if (!identityResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", identityResult.Errors.Select(e => e.Description)));
    }

    // Ánh xạ <see cref="ApplicationUser"/> sang <see cref="CurrentUserDto"/> và nạp danh sách vai trò.
    private async Task<CurrentUserDto> MapUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<CurrentUserDto>(user);
        dto.Roles = (await _userManager.GetRolesAsync(user)).ToList();
        return dto;
    }
}
