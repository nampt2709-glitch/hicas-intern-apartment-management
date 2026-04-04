using ApartmentManagement.API.V1.DTOs.Common;
using ApartmentManagement.API.V1.DTOs.Feedbacks;

namespace ApartmentManagement.API.V1.Interfaces.Services;

public interface IFeedbackService
{
    Task<PagedResultDto<FeedbackReadDto>> GetPagedAsync(PaginationQueryDto query, Guid actingUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<PagedResultDto<FeedbackReadDto>> GetMyPostsPagedAsync(PaginationQueryDto query, Guid userId, CancellationToken cancellationToken = default);
    Task<FeedbackReadDto> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<FeedbackReadDto> CreateAsync(FeedbackCreateDto dto, Guid userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<FeedbackTreeNodeDto> GetTreeAsync(Guid? rootFeedbackId, Guid actingUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackFlatDto>> GetFlattenedAsync(Guid? rootFeedbackId, Guid actingUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid actingUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
