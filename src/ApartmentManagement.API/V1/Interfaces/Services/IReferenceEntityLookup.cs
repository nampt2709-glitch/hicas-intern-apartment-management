namespace ApartmentManagement.API.V1.Interfaces.Services;

// Tra cứu thực thể và ràng buộc nghiệp vụ (trùng số căn, tháng hóa đơn, gán cư dân...) cho validator/service.
public interface IReferenceEntityLookup
{
    Task<bool> ApartmentExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UtilityServiceExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> InvoiceExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> FeedbackExistsAsync(Guid id, CancellationToken cancellationToken = default);

    // Căn hộ khác (chưa xóa) đã dùng số căn này (đã trim).
    Task<bool> IsApartmentNumberInUseAsync(string apartmentNumber, Guid? excludeApartmentId, CancellationToken cancellationToken = default);

    // Tiện ích khác đã có tên này (trim, không phân biệt hoa thường).
    Task<bool> IsUtilityServiceNameInUseAsync(string serviceName, Guid? excludeUtilityServiceId, CancellationToken cancellationToken = default);

    // Đã có hóa đơn cho cùng căn và cùng tháng thanh toán (lịch).
    Task<bool> InvoiceExistsForApartmentAndBillingMonthAsync(
        Guid apartmentId,
        DateOnly billingMonth,
        Guid? excludeInvoiceId,
        CancellationToken cancellationToken = default);

    // True nếu xung đột gán: trùng (căn,user) khi có user; hoặc user đã gắn cư dân khác;
    // khi không có user, trùng (căn, SĐT) cho cư dân khách không tài khoản.
    Task<bool> ResidentAssignmentConflictsAsync(
        Guid apartmentId,
        Guid? userId,
        string phoneNumber,
        Guid? excludeResidentId,
        CancellationToken cancellationToken = default);

    // Email chuẩn hóa chưa bị người dùng khác (chưa xóa) sử dụng.
    Task<bool> IsEmailAvailableForAnotherUserAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken = default);
}
