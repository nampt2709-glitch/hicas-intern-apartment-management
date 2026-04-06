using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.Entities;

namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Phản hồi: lấy các dòng phục vụ dựng cây theo bộ lọc FeedbackTreeFilter.
public interface IFeedbackRepository : IGenericRepository<Feedback>
{
    Task<List<FeedbackTreeRowDto>> GetFeedbackTreeRowsAsync(FeedbackTreeFilter filter, CancellationToken cancellationToken = default);
}
