// DTO phản hồi dạng danh sách phẳng (đã có depth/path để sắp xếp hiển thị).
namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackFlatDto
{
    // Mã phản hồi.
    public Guid Id { get; set; }
    // Phản hồi cha (nếu là trả lời).
    public Guid? ParentFeedbackId { get; set; }
    // Độ sâu trong luồng.
    public int Depth { get; set; }
    // Chuỗi path phân cấp.
    public string Path { get; set; } = string.Empty;
    // Nội dung.
    public string Content { get; set; } = string.Empty;
    // Thời gian tạo.
    public DateTime CreatedAt { get; set; }
}
