using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Exceptions;

// Ánh xạ lỗi EF Core / SQL Server sang ngữ nghĩa HTTP mà không reference SqlClient tại compile time.
internal static class DatabaseExceptionMapper
{
    // Mã lỗi SQL Server: trùng khóa / vi phạm unique constraint.
    private const int SqlDuplicateKey2601 = 2601;
    private const int SqlUniqueConstraint2627 = 2627;

    // Duyệt chuỗi InnerException, đọc SqlException.Number qua reflection nếu có.
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner.GetType().FullName != "Microsoft.Data.SqlClient.SqlException")
                continue;
            var number = inner.GetType().GetProperty("Number")?.GetValue(inner) as int?;
            if (number is SqlDuplicateKey2601 or SqlUniqueConstraint2627)
                return true;
        }

        return false;
    }
}
