using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository căn hộ: entity theo dõi, đếm và thêm ảnh đính kèm.
public sealed class ApartmentRepository : RepositoryBase<Apartment>, IApartmentRepository
{
    // Khởi tạo với DbContext chung.
    public ApartmentRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    // Lấy căn hộ đang theo dõi theo Id (không AsNoTracking).
    public Task<Apartment?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Db.Set<Apartment>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    // Đếm số ảnh đã gắn với căn hộ (dùng SortOrder).
    public Task<int> CountImagesForApartmentAsync(Guid apartmentId, CancellationToken cancellationToken = default)
        => Db.Set<ApartmentImage>().CountAsync(x => x.ApartmentId == apartmentId, cancellationToken);

    // Thêm bản ghi ảnh căn hộ vào DbSet.
    public Task AddApartmentImageAsync(ApartmentImage image, CancellationToken cancellationToken = default)
        => Db.Set<ApartmentImage>().AddAsync(image, cancellationToken).AsTask();
}
