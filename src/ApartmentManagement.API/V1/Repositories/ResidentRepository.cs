using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class ResidentRepository : RepositoryBase<Resident>, IResidentRepository
{
    public ResidentRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    public Task<bool> ExistsForUserAndApartmentAsync(Guid userId, Guid apartmentId, CancellationToken cancellationToken = default)
        => Db.Set<Resident>().AnyAsync(r => r.UserId == userId && r.ApartmentId == apartmentId, cancellationToken);
}
