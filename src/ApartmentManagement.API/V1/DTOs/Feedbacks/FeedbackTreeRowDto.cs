// DTO một dòng phản hồi trong cấu trúc cây phẳng (materialized path / depth).
namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackTreeRowDto
{
    // Khóa chính phản hồi.
    public Guid Id { get; set; }
    // Phản hồi cha trong luồng hội thoại (null nếu gốc).
    public Guid? ParentFeedbackId { get; set; }
    // Người viết.
    public Guid UserId { get; set; }
    // Nội dung văn bản.
    public string Content { get; set; } = string.Empty;
    // Tham chiếu căn hộ (nếu có).
    public Guid? ReferenceApartmentId { get; set; }
    // Tham chiếu hóa đơn (nếu có).
    public Guid? ReferenceInvoiceId { get; set; }
    // Chuỗi đường dẫn phân cấp (materialized path).
    public string Path { get; set; } = string.Empty;
    // Độ sâu trong cây (0 = gốc).
    public int Depth { get; set; }
    // Thời điểm tạo.
    public DateTime CreatedAt { get; set; }
}
