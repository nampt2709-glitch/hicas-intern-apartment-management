namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackTreeRowDto
{
    public Guid Id { get; set; }
    public Guid? ParentFeedbackId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ReferenceApartmentId { get; set; }
    public Guid? ReferenceInvoiceId { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Depth { get; set; }
    public DateTime CreatedAt { get; set; }
}
