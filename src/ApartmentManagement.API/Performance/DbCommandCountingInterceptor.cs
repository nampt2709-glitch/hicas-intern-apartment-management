using System.Diagnostics;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApartmentManagement.Performance;

// Interceptor EF: mỗi lệnh SQL tăng bộ đếm và cộng dồn thời gian thực thi vào RequestMetrics (scoped).
public sealed class DbCommandCountingInterceptor : DbCommandInterceptor
{
    private readonly RequestMetrics _metrics;

    public DbCommandCountingInterceptor(RequestMetrics metrics)
    {
        _metrics = metrics;
    }

    private void Count() => _metrics.IncrementDbQueryCount();

    // Khối Reader đồng bộ: đo thời gian, gọi base, cộng ms vào metrics.
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return base.ReaderExecuting(command, eventData, result);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }

    // Khối Reader bất đồng bộ (tương tự ReaderExecuting).
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }

    // Scalar đồng bộ.
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return base.ScalarExecuting(command, eventData, result);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }

    // Scalar bất đồng bộ.
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }

    // NonQuery đồng bộ (INSERT/UPDATE/DELETE...).
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return base.NonQueryExecuting(command, eventData, result);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }

    // NonQuery bất đồng bộ.
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Count();
        try
        {
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
        finally
        {
            _metrics.AddDbQueryElapsedMilliseconds(sw.ElapsedMilliseconds);
        }
    }
}
