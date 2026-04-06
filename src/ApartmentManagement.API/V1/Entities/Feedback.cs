using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Entities;

// Phản hồi dạng luồng / cây: thông báo chung hoặc trả lời lồng nhau, có thể gắn căn hộ hoặc hóa đơn.
public class Feedback : BaseEntity
{
    public Guid UserId { get; set; }
    // Điều hướng: tác giả bài viết (một user — nhiều phản hồi).
    public ApplicationUser User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public Guid? ReferenceApartmentId { get; set; }
    // Điều hướng: căn hộ được nhắc tới (tùy chọn; một căn — nhiều phản hồi tham chiếu).
    public Apartment? ReferenceApartment { get; set; }

    public Guid? ReferenceInvoiceId { get; set; }
    // Điều hướng: hóa đơn được nhắc tới (tùy chọn; một hóa đơn — nhiều phản hồi tham chiếu).
    public Invoice? ReferenceInvoice { get; set; }

    public Guid? ParentFeedbackId { get; set; }
    // Điều hướng: phản hồi cha trong luồng hội thoại (tự tham chiếu).
    public Feedback? ParentFeedback { get; set; }

    // Chuỗi materialized path phục vụ sắp xếp / truy vấn cây.
    public string Path { get; set; } = string.Empty;

    // Điều hướng: các phản hồi con trực tiếp (một cha — nhiều con).
    public ICollection<Feedback> Replies { get; set; } = new List<Feedback>();
}
