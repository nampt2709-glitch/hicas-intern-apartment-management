// DTO tạo phản hồi mới hoặc trả lời trong luồng.
namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackCreateDto
{
    // Nội dung bài viết.
    public string Content { get; set; } = string.Empty;
    // Gắn với căn hộ cụ thể (tùy chọn).
    public Guid? ReferenceApartmentId { get; set; }
    // Gắn với hóa đơn (tùy chọn).
    public Guid? ReferenceInvoiceId { get; set; }
    // Trả lời một phản hồi khác (null nếu là chủ đề mới).
    public Guid? ParentFeedbackId { get; set; }
}
