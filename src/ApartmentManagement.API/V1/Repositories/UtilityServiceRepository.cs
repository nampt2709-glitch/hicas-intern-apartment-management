using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.Data;

namespace ApartmentManagement.API.V1.Repositories;

// Repository truy cập thực thể dịch vụ tiện ích (kế thừa CRUD cơ sở).
public sealed class UtilityServiceRepository : RepositoryBase<UtilityService>, IUtilityServiceRepository
{
    // Khởi tạo với DbContext chung.
    public UtilityServiceRepository(ApartmentDbContext db)
        : base(db)
    {
    }
}
