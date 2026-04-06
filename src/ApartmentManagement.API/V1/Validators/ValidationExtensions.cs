using FluentValidation.Results;

namespace ApartmentManagement.API.V1.Validators;

// Tiện ích chuyển kết quả FluentValidation sang dictionary lỗi theo tên thuộc tính (phục vụ API).
public static class ValidationExtensions
{
    public static IDictionary<string, string[]> ToDictionary(this ValidationResult result)
        => result.Errors
                 .GroupBy(x => x.PropertyName)
                 .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray());
}
