namespace ApartmentManagement.API.V1.DTOs.Common;

public class ApiResponseDto<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}
