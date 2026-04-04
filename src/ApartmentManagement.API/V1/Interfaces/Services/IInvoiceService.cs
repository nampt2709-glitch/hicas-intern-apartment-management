using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Invoices;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IInvoiceService
{
    Task<PagedResultDto<InvoiceReadDto>> GetPagedAsync(PaginationQueryDto query, CancellationToken cancellationToken = default);
    Task<PagedResultDto<InvoiceReadDto>> GetMineForResidentAsync(PaginationQueryDto query, Guid userId, CancellationToken cancellationToken = default);
    Task<InvoiceReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<InvoiceReadDto> CreateAsync(InvoiceCreateDto dto, CancellationToken cancellationToken = default);
    Task<InvoiceReadDto> UpdateAsync(Guid id, InvoiceUpdateDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
