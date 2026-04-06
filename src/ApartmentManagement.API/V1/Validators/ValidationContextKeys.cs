namespace ApartmentManagement.API.V1.Validators;

// Khóa truyền qua RootContextData của FluentValidation (validator PUT) để biết id tuyến đường / thực thể đang sửa.
public static class ValidationContextKeys
{
    public const string ApartmentId = nameof(ApartmentId);
    public const string UtilityServiceId = nameof(UtilityServiceId);
    public const string InvoiceId = nameof(InvoiceId);
    public const string ResidentId = nameof(ResidentId);
    public const string RouteUserId = nameof(RouteUserId);
}
