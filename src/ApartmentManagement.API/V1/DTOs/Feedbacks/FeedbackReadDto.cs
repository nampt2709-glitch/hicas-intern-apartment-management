// DTO đọc phản hồi kèm tên người dùng và thông tin tham chiếu.
namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackReadDto
{
    // Khóa chính.
    public Guid Id { get; set; }
    // Người viết.
    public Guid UserId { get; set; }
    // Tên hiển thị hoặc định danh người dùng.
    public string UserName { get; set; } = string.Empty;
    // Nội dung phản hồi.
    public string Content { get; set; } = string.Empty;
    // Căn hộ được nhắc tới (nếu có).
    public Guid? ReferenceApartmentId { get; set; }
    // Hóa đơn được nhắc tới (nếu có).
    public Guid? ReferenceInvoiceId { get; set; }
    // Phản hồi cha (luồng hội thoại).
    public Guid? ParentFeedbackId { get; set; }
    // Đường dẫn trong cấu trúc cây.
    public string Path { get; set; } = string.Empty;
    // Thời điểm tạo.
    public DateTime CreatedAt { get; set; }
}
