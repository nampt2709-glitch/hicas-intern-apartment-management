// Bộ FluentValidation cho các DTO API (đăng nhập, người dùng, căn hộ, hóa đơn, phản hồi...).
// Giới hạn độ dài/giá trị và kiểm tra tham chiếu qua IReferenceEntityLookup khi cần.
using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.DTOs.Apartments;
using ApartmentManagement.API.V1.DTOs.Auth;
using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.DTOs.Invoices;
using ApartmentManagement.API.V1.DTOs.Residents;
using ApartmentManagement.API.V1.DTOs.Services;
using FluentValidation;
using FluentValidation.Validators;

namespace ApartmentManagement.API.V1.Validators;

// Hằng số giới hạn dùng chung cho phân trang và các trường số trong validator.
internal static class ValidationLimits
{
    public const int MaxPageSize = 100;
    public const decimal MaxApartmentAreaM2 = 500_000m;
    public const decimal MaxUnitPrice = 999_999_999.99m;
}

// Xác thực yêu cầu đăng nhập (email + mật khẩu).
public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);

        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
    }
}

// Xác thực cặp access token + refresh token khi làm mới phiên.
public class RefreshTokenRequestDtoValidator : AbstractValidator<RefreshTokenRequestDto>
{
    public RefreshTokenRequestDtoValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(16_384);
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(16_384);
    }
}

// Xác thực email khi yêu cầu quên mật khẩu.
public class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);
    }
}

// Xác thực đặt lại mật khẩu (email, token, mật khẩu mới).
public class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);

        RuleFor(x => x.Token).NotEmpty().MaximumLength(16_384);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256);
    }
}

// Xác thực mật khẩu mới khi admin đặt lại cho người dùng.
public class ResetUserPasswordDtoValidator : AbstractValidator<ResetUserPasswordDto>
{
    public ResetUserPasswordDtoValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256);
    }
}

public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    private const string PhonePattern = @"^\+?[0-9][0-9\-\s()]{6,28}[0-9]$";

    // Chỉ quy tắc đồng bộ (validation tự động ASP.NET không hỗ trợ async). Trùng email được kiểm tra trong AuthService.RegisterAsync.
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);

        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be 8–30 characters and contain only digits, spaces, or + - ( ).");
    }
}

public class CreateUserRequestDtoValidator : AbstractValidator<CreateUserRequestDto>
{
    private const string PhonePattern = @"^\+?[0-9][0-9\-\s()]{6,28}[0-9]$";

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase) { "Admin", "User" };

    // Chỉ quy tắc đồng bộ. Trùng email được kiểm tra trong UserService.CreateAsync.
    public CreateUserRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);

        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be 8–30 characters and contain only digits, spaces, or + - ( ).");

        RuleFor(x => x.Roles)
            .NotEmpty()
            .WithMessage("At least one role is required.");

        RuleForEach(x => x.Roles)
            .NotEmpty()
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage("Each role must be Admin or User.");

        RuleFor(x => x.Roles)
            .Must(roles => roles.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() == roles.Count)
            .WithMessage("Roles must be distinct.");
    }
}

// Xác thực tham số phân trang (trang, kích thước, tìm kiếm, sắp xếp).
public class PaginationQueryDtoValidator : AbstractValidator<PaginationQueryDto>
{
    public PaginationQueryDtoValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationLimits.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(500).When(x => x.Search is not null);
        RuleFor(x => x.SortBy).MaximumLength(64).When(x => x.SortBy is not null);
    }
}

// Xác thực cập nhật thông tin người dùng (admin), kèm kiểm tra tồn tại user và email.
public class CurrentUserDtoValidator : AbstractValidator<CurrentUserDto>
{
    public CurrentUserDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.UserExistsAsync(id, ct))
            .WithMessage("User does not exist or has been removed.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256);

        RuleFor(x => x.Email)
            .MustAsync(async (dto, email, ctx, ct) =>
            {
                var exclude = ctx.RootContextData.TryGetValue(ValidationContextKeys.RouteUserId, out var v) && v is Guid g
                    ? g
                    : dto.UserId;
                return await refs.IsEmailAvailableForAnotherUserAsync(email, exclude, ct);
            })
            .WithMessage("This email is already registered to another account.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.AvatarPath).MaximumLength(500).When(x => x.AvatarPath is not null);
    }
}

// Xác thực người dùng tự cập nhật hồ sơ (họ tên, SĐT, đổi mật khẩu).
public class UpdateMyProfileDtoValidator : AbstractValidator<UpdateMyProfileDto>
{
    private const string PhonePattern = @"^\+?[0-9][0-9\-\s()]{6,28}[0-9]$";

    public UpdateMyProfileDtoValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phone number must be 8–30 characters and contain only digits, spaces, or + - ( ).");

        RuleFor(x => x.NewPassword)
            .MinimumLength(8)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword))
            .WithMessage("Current password is required when setting a new password.");
    }
}

// Xác thực tạo căn hộ (số căn duy nhất, tầng, diện tích, trạng thái).
public class ApartmentCreateDtoValidator : AbstractValidator<ApartmentCreateDto>
{
    public ApartmentCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentNumber)
            .NotEmpty()
            .MaximumLength(50)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.ApartmentNumber)
            .MustAsync(async (num, ct) => !await refs.IsApartmentNumberInUseAsync(num, null, ct))
            .WithMessage("An apartment with this number already exists.");

        RuleFor(x => x.Floor).InclusiveBetween(-5, 200);
        RuleFor(x => x.Area).InclusiveBetween(0.01m, ValidationLimits.MaxApartmentAreaM2);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}

// Xác thực cập nhật căn hộ (bỏ qua trùng số căn với chính bản ghi hiện tại qua RootContextData).
public class ApartmentUpdateDtoValidator : AbstractValidator<ApartmentUpdateDto>
{
    public ApartmentUpdateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentNumber)
            .NotEmpty()
            .MaximumLength(50)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.ApartmentNumber)
            .MustAsync(async (dto, num, ctx, ct) =>
            {
                if (!ctx.RootContextData.TryGetValue(ValidationContextKeys.ApartmentId, out var v) || v is not Guid id)
                    return true;
                return !await refs.IsApartmentNumberInUseAsync(num, id, ct);
            })
            .WithMessage("An apartment with this number already exists.");

        RuleFor(x => x.Floor).InclusiveBetween(-5, 200);
        RuleFor(x => x.Area).InclusiveBetween(0.01m, ValidationLimits.MaxApartmentAreaM2);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
    }
}

// Xác thực tạo cư dân (căn hộ, user tùy chọn, xung đột gán qua refs).
public class ResidentCreateDtoValidator : AbstractValidator<ResidentCreateDto>
{
    private const string PhonePattern = @"^\+?[0-9][0-9\-\s()]{6,28}[0-9]$";

    public ResidentCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.ApartmentExistsAsync(id, ct))
            .WithMessage("Apartment does not exist or has been removed.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .When(x => x.UserId.HasValue);

        RuleFor(x => x.UserId)
            .MustAsync(async (uid, ct) => !uid.HasValue || await refs.UserExistsAsync(uid.Value, ct))
            .When(x => x.UserId.HasValue)
            .WithMessage("User does not exist or has been removed.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(PhonePattern)
            .WithMessage("Phone number must be 8–30 characters and contain only digits, spaces, or + - ( ).");

        RuleFor(x => x.Email)
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x)
            .CustomAsync(async (dto, ctx, ct) =>
            {
                if (await refs.ResidentAssignmentConflictsAsync(dto.ApartmentId, dto.UserId, dto.PhoneNumber, null, ct))
                    ctx.AddFailure(nameof(dto.UserId), "This apartment and user (or phone for a guest resident) is already linked to another resident record.");
            });
    }
}

// Xác thực cập nhật cư dân (ngày chuyển đi so với chuyển đến, xung đột gán).
public class ResidentUpdateDtoValidator : AbstractValidator<ResidentUpdateDto>
{
    private const string PhonePattern = @"^\+?[0-9][0-9\-\s()]{6,28}[0-9]$";

    public ResidentUpdateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.ApartmentExistsAsync(id, ct))
            .WithMessage("Apartment does not exist or has been removed.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .When(x => x.UserId.HasValue);

        RuleFor(x => x.UserId)
            .MustAsync(async (uid, ct) => !uid.HasValue || await refs.UserExistsAsync(uid.Value, ct))
            .When(x => x.UserId.HasValue)
            .WithMessage("User does not exist or has been removed.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(PhonePattern)
            .WithMessage("Phone number must be 8–30 characters and contain only digits, spaces, or + - ( ).");

        RuleFor(x => x.Email)
            .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.MoveOutDate)
            .GreaterThanOrEqualTo(x => x.MoveInDate)
            .When(x => x.MoveOutDate.HasValue);

        RuleFor(x => x)
            .CustomAsync(async (dto, ctx, ct) =>
            {
                if (!ctx.RootContextData.TryGetValue(ValidationContextKeys.ResidentId, out var v) || v is not Guid rid)
                    return;
                if (await refs.ResidentAssignmentConflictsAsync(dto.ApartmentId, dto.UserId, dto.PhoneNumber, rid, ct))
                    ctx.AddFailure(nameof(dto.UserId), "This apartment and user (or phone for a guest resident) is already linked to another resident record.");
            });
    }
}

// Xác thực tạo dịch vụ tiện ích (tên duy nhất, đơn vị, đơn giá).
public class UtilityServiceCreateDtoValidator : AbstractValidator<UtilityServiceCreateDto>
{
    public UtilityServiceCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.ServiceName)
            .MustAsync(async (name, ct) => !await refs.IsUtilityServiceNameInUseAsync(name, null, ct))
            .WithMessage("A utility with this name already exists.");

        RuleFor(x => x.Unit)
            .NotEmpty()
            .MaximumLength(50)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.UnitPrice).InclusiveBetween(0, ValidationLimits.MaxUnitPrice);
    }
}

// Xác thực cập nhật dịch vụ tiện ích (tên không trùng bản ghi khác).
public class UtilityServiceUpdateDtoValidator : AbstractValidator<UtilityServiceUpdateDto>
{
    public UtilityServiceUpdateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.ServiceName)
            .MustAsync(async (dto, name, ctx, ct) =>
            {
                if (!ctx.RootContextData.TryGetValue(ValidationContextKeys.UtilityServiceId, out var v) || v is not Guid id)
                    return true;
                return !await refs.IsUtilityServiceNameInUseAsync(name, id, ct);
            })
            .WithMessage("A utility with this name already exists.");

        RuleFor(x => x.Unit)
            .NotEmpty()
            .MaximumLength(50)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.UnitPrice).InclusiveBetween(0, ValidationLimits.MaxUnitPrice);
    }
}

// Xác thực một dòng chi tiết hóa đơn (dịch vụ, số lượng, đơn giá).
public class InvoiceDetailCreateDtoValidator : AbstractValidator<InvoiceDetailCreateDto>
{
    public InvoiceDetailCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.UtilityServiceId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.UtilityServiceExistsAsync(id, ct))
            .WithMessage("Utility service does not exist or has been removed.");

        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000m);
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, ValidationLimits.MaxUnitPrice);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}

// Xác thực tạo hóa đơn (tháng thanh toán duy nhất theo căn, chi tiết không trùng dịch vụ).
public class InvoiceCreateDtoValidator : AbstractValidator<InvoiceCreateDto>
{
    public InvoiceCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.ApartmentExistsAsync(id, ct))
            .WithMessage("Apartment does not exist or has been removed.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.BillingMonth)
            .Must(d => d.Year is >= 2000 and <= 2100)
            .WithMessage("Billing month year must be between 2000 and 2100.");

        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate);

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("At least one invoice line is required.");

        RuleFor(x => x.Details)
            .Must(lines => lines.TrueForAll(l => l is not null))
            .When(x => x.Details.Count > 0);

        RuleFor(x => x.Details)
            .Must(lines => lines.GroupBy(l => l.UtilityServiceId).All(g => g.Count() == 1))
            .When(x => x.Details.Count > 0)
            .WithMessage("Each utility service may appear only once per invoice.");

        RuleForEach(x => x.Details).SetValidator(new InvoiceDetailCreateDtoValidator(refs));

        RuleFor(x => x)
            .CustomAsync(async (dto, ctx, ct) =>
            {
                if (await refs.InvoiceExistsForApartmentAndBillingMonthAsync(dto.ApartmentId, dto.BillingMonth, null, ct))
                    ctx.AddFailure(nameof(dto.BillingMonth), "An invoice for this apartment and billing month already exists.");
            });
    }
}

// Xác thực cập nhật hóa đơn (tương tự tạo, loại trừ id hóa đơn hiện tại khi kiểm tra tháng).
public class InvoiceUpdateDtoValidator : AbstractValidator<InvoiceUpdateDto>
{
    public InvoiceUpdateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.ApartmentId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await refs.ApartmentExistsAsync(id, ct))
            .WithMessage("Apartment does not exist or has been removed.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.BillingMonth)
            .Must(d => d.Year is >= 2000 and <= 2100)
            .WithMessage("Billing month year must be between 2000 and 2100.");

        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate);

        RuleFor(x => x.Details)
            .NotEmpty()
            .WithMessage("At least one invoice line is required.");

        RuleFor(x => x.Details)
            .Must(lines => lines.TrueForAll(l => l is not null))
            .When(x => x.Details.Count > 0);

        RuleFor(x => x.Details)
            .Must(lines => lines.GroupBy(l => l.UtilityServiceId).All(g => g.Count() == 1))
            .When(x => x.Details.Count > 0)
            .WithMessage("Each utility service may appear only once per invoice.");

        RuleForEach(x => x.Details).SetValidator(new InvoiceDetailCreateDtoValidator(refs));

        RuleFor(x => x)
            .CustomAsync(async (dto, ctx, ct) =>
            {
                if (!ctx.RootContextData.TryGetValue(ValidationContextKeys.InvoiceId, out var v) || v is not Guid invId)
                    return;
                if (await refs.InvoiceExistsForApartmentAndBillingMonthAsync(dto.ApartmentId, dto.BillingMonth, invId, ct))
                    ctx.AddFailure(nameof(dto.BillingMonth), "An invoice for this apartment and billing month already exists.");
            });
    }
}

// Xác thực tạo phản hồi (nội dung, phản hồi cha, tham chiếu căn/hóa đơn tùy chọn).
public class FeedbackCreateDtoValidator : AbstractValidator<FeedbackCreateDto>
{
    public FeedbackCreateDtoValidator(IReferenceEntityLookup refs)
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(s => !string.IsNullOrWhiteSpace(s));

        RuleFor(x => x.ParentFeedbackId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ParentFeedbackId cannot be empty GUID.")
            .MustAsync(async (id, ct) => !id.HasValue || await refs.FeedbackExistsAsync(id.Value, ct))
            .WithMessage("Parent feedback does not exist or has been removed.");

        RuleFor(x => x.ReferenceApartmentId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ReferenceApartmentId cannot be empty GUID.")
            .MustAsync(async (id, ct) => !id.HasValue || await refs.ApartmentExistsAsync(id.Value, ct))
            .WithMessage("Referenced apartment does not exist or has been removed.");

        RuleFor(x => x.ReferenceInvoiceId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ReferenceInvoiceId cannot be empty GUID.")
            .MustAsync(async (id, ct) => !id.HasValue || await refs.InvoiceExistsAsync(id.Value, ct))
            .WithMessage("Referenced invoice does not exist or has been removed.");
    }
}
