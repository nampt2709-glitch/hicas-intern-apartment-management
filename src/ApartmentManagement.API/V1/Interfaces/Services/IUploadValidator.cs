using ApartmentManagement.API.V1.DTOs.Uploads;
using Microsoft.AspNetCore.Http;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IUploadValidator
{
    Task ValidateRequestAsync(IReadOnlyCollection<IFormFile> files, CancellationToken cancellationToken = default);
    Task ValidateImageAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>User avatar under <c>avatars/{userId:N}/</c> (replaces previous file in that folder).</summary>
    Task<UploadResultDto> SaveUserAvatarAsync(IFormFile file, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Apartment gallery image under <c>apartmentImages/{apartmentId:N}/</c>.</summary>
    Task<UploadResultDto> SaveApartmentImageAsync(IFormFile file, Guid apartmentId, CancellationToken cancellationToken = default);
}
