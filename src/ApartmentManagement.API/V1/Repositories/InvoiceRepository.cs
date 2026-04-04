using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class InvoiceRepository : RepositoryBase<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    public Task<Invoice?> GetTrackedWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>()
            .Include(x => x.InvoiceDetails)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void RemoveAllInvoiceDetails(Invoice trackedInvoice)
    {
        var existing = trackedInvoice.InvoiceDetails.ToList();
        Db.Set<InvoiceDetail>().RemoveRange(existing);
        trackedInvoice.InvoiceDetails.Clear();
    }

    public Task<Invoice> GetWithReadGraphAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>().AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Apartment)
            .Include(x => x.InvoiceDetails)
            .ThenInclude(x => x.UtilityService)
            .FirstAsync(x => x.Id == id, cancellationToken);

    public Task<Guid?> GetApartmentIdByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>().AsNoTracking()
            .Where(x => x.Id == invoiceId)
            .Select(x => (Guid?)x.ApartmentId)
            .FirstOrDefaultAsync(cancellationToken);
}
