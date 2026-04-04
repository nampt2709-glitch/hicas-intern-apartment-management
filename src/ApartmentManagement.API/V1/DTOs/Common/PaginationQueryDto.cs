namespace ApartmentManagement.API.V1.DTOs.Common;

public class PaginationQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool Descending { get; set; } = false;
    public bool IncludeDeleted { get; set; } = false;
}
