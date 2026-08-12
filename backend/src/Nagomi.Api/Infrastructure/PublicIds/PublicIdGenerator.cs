using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Persistence;

namespace Nagomi.Api.Infrastructure.PublicIds;

internal sealed class PublicIdCounterResult
{
    public long LastValue { get; set; }
}

public interface IPublicIdGenerator
{
    Task<string> NextAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>
    /// Reserva <paramref name="count"/> identificadores consecutivos del prefijo
    /// (para recurrencias que materializan muchos trayectos de una vez).
    /// </summary>
    Task<IReadOnlyList<string>> NextBatchAsync(string prefix, int count, CancellationToken cancellationToken);
}

/// <summary>
/// Genera identificadores públicos legibles para humanos, secuenciales por año:
/// <c>REQ-2026-000001</c>, <c>JRN-2026-000042</c>, <c>EMG-2026-000017</c>.
/// El contador se guarda en <c>public_id_counters</c> con un UPSERT atómico
/// (ON CONFLICT … RETURNING), de modo que dos peticiones concurrentes nunca
/// obtienen el mismo número para el mismo prefijo y año.
/// </summary>
public sealed class PostgresPublicIdGenerator(NagomiDbContext db) : IPublicIdGenerator
{
    public async Task<string> NextAsync(string prefix, CancellationToken cancellationToken)
    {
        var batch = await NextBatchAsync(prefix, 1, cancellationToken);
        return batch[0];
    }

    public async Task<IReadOnlyList<string>> NextBatchAsync(string prefix, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return Array.Empty<string>();
        var year = DateTime.UtcNow.Year;
        var sql = """
            INSERT INTO public_id_counters (prefix, year, last_value)
            VALUES ({0}, {1}, {2})
            ON CONFLICT (prefix, year)
            DO UPDATE SET last_value = public_id_counters.last_value + {2}
            RETURNING last_value AS "LastValue";
            """;
        // SqlQueryRaw devuelve un IQueryable que EF intentaría componer; el SQL
        // (INSERT … ON CONFLICT … RETURNING) no es composable, así que se materializa
        // en cliente con AsEnumerable(). El UPSERT es atómico: aunque haya N
        // conexiones pidiendo ID a la vez, cada una recibe un número distinto.
        var result = db.Database
            .SqlQueryRaw<PublicIdCounterResult>(sql, prefix, year, count)
            .AsEnumerable()
            .Single();
        var start = result.LastValue - count + 1;
        return Enumerable.Range(0, count)
            .Select(i => $"{prefix}-{year}-{start + i:000000}")
            .ToArray();
    }
}
