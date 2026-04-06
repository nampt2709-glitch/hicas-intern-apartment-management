// DTO nút phản hồi dạng cây (có danh sách phản hồi con lồng nhau).
namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackTreeNodeDto
{
    // Mã phản hồi.
    public Guid Id { get; set; }
    // Phản hồi cha (null nếu là chủ đề gốc).
    public Guid? ParentFeedbackId { get; set; }
    // Người tạo.
    public Guid UserId { get; set; }
    // Nội dung.
    public string Content { get; set; } = string.Empty;
    // Liên kết tới căn hộ (tùy chọn).
    public Guid? ReferenceApartmentId { get; set; }
    // Liên kết tới hóa đơn (tùy chọn).
    public Guid? ReferenceInvoiceId { get; set; }
    // Đường dẫn phân cấp trong cây.
    public string Path { get; set; } = string.Empty;
    // Độ sâu so với gốc.
    public int Depth { get; set; }
    // Thời gian tạo bản ghi.
    public DateTime CreatedAt { get; set; }
    // Các phản hồi trả lời trực tiếp nút này.
    public List<FeedbackTreeNodeDto> Replies { get; set; } = new();
}
