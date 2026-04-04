using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.Data;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class UtilityServiceRepository : RepositoryBase<UtilityService>, IUtilityServiceRepository
{
    public UtilityServiceRepository(ApartmentDbContext db)
        : base(db)
    {
    }
}
