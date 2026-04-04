using ApartmentManagement.API.V1.Entities.Security;

namespace ApartmentManagement.API.V1.Entities;

/// <summary>Threaded feedback (building notices, replies) linked to users and optionally apartments/invoices.</summary>
public class Feedback : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public Guid? ReferenceApartmentId { get; set; }
    public Apartment? ReferenceApartment { get; set; }

    public Guid? ReferenceInvoiceId { get; set; }
    public Invoice? ReferenceInvoice { get; set; }

    public Guid? ParentFeedbackId { get; set; }
    public Feedback? ParentFeedback { get; set; }

    public string Path { get; set; } = string.Empty;

    public ICollection<Feedback> Replies { get; set; } = new List<Feedback>();
}
