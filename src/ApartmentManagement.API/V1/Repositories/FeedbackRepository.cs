using ApartmentManagement.API.V1.DTOs.Feedbacks;
using ApartmentManagement.API.V1.Interfaces.Repositories;
using ApartmentManagement.API.V1.Entities;
using ApartmentManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.API.V1.Repositories;

// Repository phản hồi: cây đệ quy SQL (CTE) trả về các dòng phẳng có độ sâu.
public sealed class FeedbackRepository : RepositoryBase<Feedback>, IFeedbackRepository
{
    // Khởi tạo với DbContext chung.
    public FeedbackRepository(ApartmentDbContext db)
        : base(db)
    {
    }

    // Thực thi truy vấn đệ quy trên Feedbacks theo bộ lọc (gốc toàn cục, gốc một luồng, giới hạn user).
    public Task<List<FeedbackTreeRowDto>> GetFeedbackTreeRowsAsync(
        FeedbackTreeFilter filter,
        CancellationToken cancellationToken = default)
    {
        var anchorAllRoots = filter.AllRootThreads ? 1 : 0;
        var restrictToOwner = filter.RestrictToActingUserOnly ? 1 : 0;
        var scopedRootId = filter.ScopeRootId;
        var actingUserId = filter.ActingUserId;

        return Db.Database.SqlQuery<FeedbackTreeRowDto>($@"
;WITH FeedbackCte AS (
    SELECT
        f.Id,
        f.ParentFeedbackId,
        f.UserId,
        f.Content,
        f.ReferenceApartmentId,
        f.ReferenceInvoiceId,
        f.Path,
        0 AS Depth,
        f.CreatedAt
    FROM Feedbacks f
    WHERE f.IsDeleted = 0
      AND (({anchorAllRoots} = 1 AND f.ParentFeedbackId IS NULL AND ({restrictToOwner} = 0 OR f.UserId = {actingUserId}))
        OR ({anchorAllRoots} = 0 AND f.Id = {scopedRootId}))

    UNION ALL

    SELECT
        child.Id,
        child.ParentFeedbackId,
        child.UserId,
        child.Content,
        child.ReferenceApartmentId,
        child.ReferenceInvoiceId,
        child.Path,
        parent.Depth + 1 AS Depth,
        child.CreatedAt
    FROM Feedbacks child
    INNER JOIN FeedbackCte parent ON child.ParentFeedbackId = parent.Id
    WHERE child.IsDeleted = 0
)
SELECT Id, ParentFeedbackId, UserId, Content, ReferenceApartmentId, ReferenceInvoiceId, Path, Depth, CreatedAt
FROM FeedbackCte
ORDER BY Path
OPTION (MAXRECURSION 32767);")
            .ToListAsync(cancellationToken);
    }
}
