using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Exceptions;

/// <summary>
/// Maps EF Core / SQL Server failures to HTTP semantics without coupling to SqlClient at compile time.
/// </summary>
internal static class DatabaseExceptionMapper
{
    /// <summary>SQL Server: cannot insert duplicate key row / unique constraint.</summary>
    private const int SqlDuplicateKey2601 = 2601;
    private const int SqlUniqueConstraint2627 = 2627;

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
