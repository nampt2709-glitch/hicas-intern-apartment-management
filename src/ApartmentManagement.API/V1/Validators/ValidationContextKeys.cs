namespace ApartmentManagement.API.V1.Validators;

/// <summary>Keys for <see cref="FluentValidation.ValidationContext{T}.RootContextData"/> on PUT validators.</summary>
public static class ValidationContextKeys
{
    public const string ApartmentId = nameof(ApartmentId);
    public const string UtilityServiceId = nameof(UtilityServiceId);
    public const string InvoiceId = nameof(InvoiceId);
    public const string ResidentId = nameof(ResidentId);
    public const string RouteUserId = nameof(RouteUserId);
}
