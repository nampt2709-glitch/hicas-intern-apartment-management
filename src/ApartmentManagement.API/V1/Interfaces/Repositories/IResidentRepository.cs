using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Repository cư dân + kiểm tra tồn tại theo user và căn hộ.
public interface IResidentRepository : IGenericRepository<Resident>
{
    Task<bool> ExistsForUserAndApartmentAsync(Guid userId, Guid apartmentId, CancellationToken cancellationToken = default);
}
