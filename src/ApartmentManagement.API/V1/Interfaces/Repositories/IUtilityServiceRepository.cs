using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Repository cho bảng tiện ích (kế thừa generic CRUD).
public interface IUtilityServiceRepository : IGenericRepository<UtilityService>
{
}
