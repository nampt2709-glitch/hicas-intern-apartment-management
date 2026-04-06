using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository hóa đơn: theo dõi chi tiết, tải graph đọc và tra cứu căn hộ theo hóa đơn.
public sealed class InvoiceRepository : RepositoryBase<Invoice>, IInvoiceRepository
{
    // Khởi tạo với DbContext chung.
    public InvoiceRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    // Lấy hóa đơn đang theo dõi kèm chi tiết (để cập nhật).
    public Task<Invoice?> GetTrackedWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>()
            .Include(x => x.InvoiceDetails)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    // Xóa toàn bộ dòng chi tiết khỏi DbContext và collection điều hướng.
    public void RemoveAllInvoiceDetails(Invoice trackedInvoice)
    {
        var existing = trackedInvoice.InvoiceDetails.ToList();
        Db.Set<InvoiceDetail>().RemoveRange(existing);
        trackedInvoice.InvoiceDetails.Clear();
    }

    // Tải hóa đơn không tracking kèm căn hộ, chi tiết và dịch vụ (split query).
    public Task<Invoice> GetWithReadGraphAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>().AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Apartment)
            .Include(x => x.InvoiceDetails)
            .ThenInclude(x => x.UtilityService)
            .FirstAsync(x => x.Id == id, cancellationToken);

    // Lấy ApartmentId của hóa đơn (dùng kiểm tra quyền tham chiếu phản hồi).
    public Task<Guid?> GetApartmentIdByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        => Db.Set<Invoice>().AsNoTracking()
            .Where(x => x.Id == invoiceId)
            .Select(x => (Guid?)x.ApartmentId)
            .FirstOrDefaultAsync(cancellationToken);
}
