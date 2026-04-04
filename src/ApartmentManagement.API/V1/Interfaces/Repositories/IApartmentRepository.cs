using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

public interface IApartmentRepository : IGenericRepository<Apartment>
{
    Task<Apartment?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountImagesForApartmentAsync(Guid apartmentId, CancellationToken cancellationToken = default);

    Task AddApartmentImageAsync(ApartmentImage image, CancellationToken cancellationToken = default);
}
