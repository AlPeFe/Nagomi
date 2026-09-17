using Microsoft.EntityFrameworkCore;

namespace Nagomi.Api.Features.ProviderIntegration;

/// <summary>
/// El proveedor PROPIO de la instalación: la flota con la que ejecuta sus traslados.
///
/// Antes esto era el «auto-proveedor SELF», que se creaba al arrancar y además se buscaba por el
/// código literal "SELF" desde tres sitios. Al dejar de sembrarlo, una instalación nueva se quedaba
/// sin proveedor y esas pantallas se rompían (los vehículos devolvían 404). Ahora:
///
///  - se resuelve sin depender del código: vale el proveedor propio ("PROPIO") o, en instalaciones
///    antiguas, el auto-proveedor "SELF" que ya exista;
///  - se crea SÓLO cuando hace falta (al dar de alta el primer vehículo), para no meter un proveedor
///    fantasma en una instalación que no ejecuta flota.
/// </summary>
public static class FleetProvider
{
    /// <summary>Código con el que se crea la flota propia.</summary>
    public const string OwnCode = "PROPIO";

    /// <summary>Código del auto-proveedor antiguo; se sigue respetando si ya existe.</summary>
    public const string LegacySelfCode = "SELF";

    public static Task<TransportProvider?> FindAsync(
        IProviderIntegrationDb db, CancellationToken cancellationToken) =>
        db.TransportProviders.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Code == LegacySelfCode || x.Code == OwnCode)
            .ThenBy(x => x.Code)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<TransportProvider> EnsureAsync(
        IProviderIntegrationDb db, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(db, cancellationToken);
        if (existing is not null)
            return existing;

        var provider = new TransportProvider
        {
            Code = OwnCode,
            Name = "Mi flota",
            QueueName = "nagomi.propio",
            IsActive = true
        };
        db.TransportProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return provider;
    }
}
