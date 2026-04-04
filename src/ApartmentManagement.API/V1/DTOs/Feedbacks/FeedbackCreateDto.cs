namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackCreateDto
{
    public string Content { get; set; } = string.Empty;
    public Guid? ReferenceApartmentId { get; set; }
    public Guid? ReferenceInvoiceId { get; set; }
    public Guid? ParentFeedbackId { get; set; }
}
