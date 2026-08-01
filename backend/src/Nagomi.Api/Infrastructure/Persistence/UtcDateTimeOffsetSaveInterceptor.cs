using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Nagomi.Api.Infrastructure.Persistence;

/// <summary>
/// Normalizes every DateTimeOffset written to a PostgreSQL 'timestamp with time zone' column
/// to UTC (offset 0). Npgsql rejects non-zero offsets on timestamptz, and since the column
/// already stores the absolute instant, converting to UTC preserves the value while keeping
/// the write compatible.
/// </summary>
public sealed class UtcDateTimeOffsetSaveInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Normalize(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Normalize(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Normalize(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var properties = entry.Properties
                .Where(p => p.CurrentValue is DateTimeOffset)
                .ToList();

            foreach (var property in properties)
            {
                if (property.CurrentValue is not DateTimeOffset value)
                    continue;

                var utc = value.ToUniversalTime();
                // Npgsql requires the offset to be exactly 0 (UTC).
                var normalized = new DateTimeOffset(utc.Ticks, TimeSpan.Zero);
                if (normalized != value)
                {
                    property.CurrentValue = normalized;
                    property.IsModified = true;
                }
            }
        }
    }
}
