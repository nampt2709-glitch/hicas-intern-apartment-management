using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository cư dân và kiểm tra tồn tại theo user và căn hộ.
public sealed class ResidentRepository : RepositoryBase<Resident>, IResidentRepository
{
    // Khởi tạo với DbContext chung.
    public ResidentRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    // Kiểm tra cư dân có liên kết user với căn hộ cho trước hay không.
    public Task<bool> ExistsForUserAndApartmentAsync(Guid userId, Guid apartmentId, CancellationToken cancellationToken = default)
        => Db.Set<Resident>().AnyAsync(r => r.UserId == userId && r.ApartmentId == apartmentId, cancellationToken);
}
