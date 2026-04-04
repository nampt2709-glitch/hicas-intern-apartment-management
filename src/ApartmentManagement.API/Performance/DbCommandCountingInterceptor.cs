using System.Diagnostics;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApartmentManagement.Performance;

public sealed class DbCommandCountingInterceptor : DbCommandInterceptor
{
    private readonly RequestMetrics _metrics;

    public DbCommandCountingInterceptor(RequestMetrics metrics)
    {
        _metrics = metrics;
    }

    private void Count() => _metrics.IncrementDbQueryCount();

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
