namespace ApartmentManagement.API.V1.Interfaces.Services;

/// <summary>Referenced-entity checks and business-level uniqueness (replacing DB-only unique indexes where applicable).</summary>
public interface IReferenceEntityLookup
{
    Task<bool> ApartmentExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UtilityServiceExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> InvoiceExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> FeedbackExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Another non-deleted apartment already uses this number (trimmed).</summary>
    Task<bool> IsApartmentNumberInUseAsync(string apartmentNumber, Guid? excludeApartmentId, CancellationToken cancellationToken = default);

    /// <summary>Another utility already has this name (trimmed, case-insensitive).</summary>
    Task<bool> IsUtilityServiceNameInUseAsync(string serviceName, Guid? excludeUtilityServiceId, CancellationToken cancellationToken = default);

    /// <summary>Invoice for same apartment and calendar billing month already exists.</summary>
    Task<bool> InvoiceExistsForApartmentAndBillingMonthAsync(
        Guid apartmentId,
        DateOnly billingMonth,
        Guid? excludeInvoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True if assignment conflicts: duplicate (apartment,user) when user is set; duplicate user on another resident;
    /// when user is null, duplicate (apartment, phone) for residents without linked user.
    /// </summary>
    Task<bool> ResidentAssignmentConflictsAsync(
        Guid apartmentId,
        Guid? userId,
        string phoneNumber,
        Guid? excludeResidentId,
        CancellationToken cancellationToken = default);

    /// <summary>Normalized email is not taken by another non-deleted user.</summary>
    Task<bool> IsEmailAvailableForAnotherUserAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default);
}
