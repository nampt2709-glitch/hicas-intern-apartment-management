namespace ApartmentManagement.API.V1.Interfaces.Repositories;

// Bộ lọc tải cây phản hồi: AllRootThreads = true: mọi luồng gốc; false: cây con từ ScopeRootId.
// RestrictToActingUserOnly: khi liệt kê gốc, chỉ luồng do ActingUserId tạo.
public readonly record struct FeedbackTreeFilter(
    bool AllRootThreads,
    Guid ScopeRootId,
    bool RestrictToActingUserOnly,
    Guid ActingUserId);
