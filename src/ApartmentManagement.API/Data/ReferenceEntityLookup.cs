using ApartmentManagement.API.V1.Interfaces.Services;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Entities.Security;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data;

public sealed class ReferenceEntityLookup : IReferenceEntityLookup
{
    private readonly ApartmentDbContext _db;

    public ReferenceEntityLookup(ApartmentDbContext db) => _db = db;

    public Task<bool> ApartmentExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Apartments.AsNoTracking().AnyAsync(a => a.Id == id, cancellationToken);

    public Task<bool> UserExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<ApplicationUser>().AsNoTracking().AnyAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);

    public Task<bool> UtilityServiceExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.UtilityServices.AsNoTracking().AnyAsync(u => u.Id == id, cancellationToken);

    public Task<bool> InvoiceExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Invoices.AsNoTracking().AnyAsync(i => i.Id == id, cancellationToken);

    public Task<bool> FeedbackExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Feedbacks.AsNoTracking().AnyAsync(f => f.Id == id, cancellationToken);

    public Task<bool> IsApartmentNumberInUseAsync(string apartmentNumber, Guid? excludeApartmentId, CancellationToken cancellationToken = default)
    {
        var n = apartmentNumber.Trim();
        var q = _db.Apartments.AsNoTracking().Where(a => a.ApartmentNumber == n);
        if (excludeApartmentId.HasValue)
            q = q.Where(a => a.Id != excludeApartmentId.Value);
        return q.AnyAsync(cancellationToken);
    }

    public Task<bool> IsUtilityServiceNameInUseAsync(string serviceName, Guid? excludeUtilityServiceId, CancellationToken cancellationToken = default)
    {
        var n = serviceName.Trim();
        var q = _db.UtilityServices.AsNoTracking().Where(u => u.ServiceName.ToLower() == n.ToLower());
        if (excludeUtilityServiceId.HasValue)
            q = q.Where(u => u.Id != excludeUtilityServiceId.Value);
        return q.AnyAsync(cancellationToken);
    }

    public Task<bool> InvoiceExistsForApartmentAndBillingMonthAsync(
        Guid apartmentId,
        DateOnly billingMonth,
        Guid? excludeInvoiceId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Invoices.AsNoTracking()
            .Where(i => i.ApartmentId == apartmentId && i.BillingMonth == billingMonth);
        if (excludeInvoiceId.HasValue)
            q = q.Where(i => i.Id != excludeInvoiceId.Value);
        return q.AnyAsync(cancellationToken);
    }

    public async Task<bool> ResidentAssignmentConflictsAsync(
        Guid apartmentId,
        Guid? userId,
        string phoneNumber,
        Guid? excludeResidentId,
        CancellationToken cancellationToken = default)
    {
        var phone = phoneNumber.Trim();
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var dupAptUser = await _db.Residents.AsNoTracking()
                .AnyAsync(r => (!excludeResidentId.HasValue || r.Id != excludeResidentId.Value) && !r.IsDeleted
                    && r.ApartmentId == apartmentId && r.UserId == userId, cancellationToken);
            if (dupAptUser)
                return true;

            var dupUserElsewhere = await _db.Residents.AsNoTracking()
                .AnyAsync(r => (!excludeResidentId.HasValue || r.Id != excludeResidentId.Value) && !r.IsDeleted && r.UserId == userId, cancellationToken);
            if (dupUserElsewhere)
                return true;
        }
        else
        {
            var dupPhone = await _db.Residents.AsNoTracking()
                .AnyAsync(r => (!excludeResidentId.HasValue || r.Id != excludeResidentId.Value) && !r.IsDeleted
                    && r.ApartmentId == apartmentId && r.UserId == null && r.PhoneNumber == phone, cancellationToken);
            if (dupPhone)
                return true;
        }

        return false;
    }

    public async Task<bool> IsEmailAvailableForAnotherUserAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();
        var q = _db.Set<ApplicationUser>().AsNoTracking()
            .Where(u => !u.IsDeleted && u.NormalizedEmail == normalized);
        if (excludeUserId.HasValue)
            q = q.Where(u => u.Id != excludeUserId.Value);
        var taken = await q.AnyAsync(cancellationToken);
        return !taken;
    }
}
