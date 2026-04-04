using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

public sealed class ApartmentRepository : RepositoryBase<Apartment>, IApartmentRepository
{
    public ApartmentRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    public Task<Apartment?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Apartment>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<int> CountImagesForApartmentAsync(Guid apartmentId, CancellationToken cancellationToken = default)
        => Db.Set<ApartmentImage>().CountAsync(x => x.ApartmentId == apartmentId, cancellationToken);

    public Task AddApartmentImageAsync(ApartmentImage image, CancellationToken cancellationToken = default)
        => Db.Set<ApartmentImage>().AddAsync(image, cancellationToken).AsTask();
}
