using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Hóa đơn: tải theo dõi kèm chi tiết, xóa chi tiết, đồ thị đọc, tra cứu căn theo hóa đơn.
public interface IInvoiceRepository : IGenericRepository<Invoice>
{
    Task<Invoice?> GetTrackedWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    void RemoveAllInvoiceDetails(Invoice trackedInvoice);

    Task<Invoice> GetWithReadGraphAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid?> GetApartmentIdByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
