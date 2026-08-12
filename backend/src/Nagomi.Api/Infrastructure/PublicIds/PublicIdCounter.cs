namespace Nagomi.Api.Infrastructure.PublicIds;

/// <summary>
/// Contador persistente para identificadores públicos legibles por humanos.
/// Una fila por (prefijo, año) — el número reinicia cada año.
/// </summary>
public sealed class PublicIdCounter
{
    public string Prefix { get; set; } = "";
    public int Year { get; set; }
    public long LastValue { get; set; }
}
