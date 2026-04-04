namespace ApartmentManagement.API.V1.DTOs.Feedbacks;

public class FeedbackFlatDto
{
    public Guid Id { get; set; }
    public Guid? ParentFeedbackId { get; set; }
    public int Depth { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
