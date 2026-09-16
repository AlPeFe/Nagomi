namespace Nagomi.Api.Features.ProviderIntegration;

/// <summary>
/// Decide si un traslado adjudicado debe publicarse en alguna cola de Rabbit.
///
/// - Empresa de ambulancias (la instalación ejecuta sus propios traslados): se publica en la
///   cola de su flota, porque el cliente es informativo y no define cola.
/// - Instalación que sólo publica a un proveedor externo: el destino lo define el CLIENTE. Si el
///   cliente no tiene cola, NO se publica en ninguna cola: el traslado sigue expuesto por la API
///   con normalidad (es una fuente de verdad, la cola sólo un canal de aviso).
/// </summary>
public static class PublicationPolicy
{
    public static bool ShouldPublish(bool tenantExecutesOwnFleet, string? clientQueue) =>
        tenantExecutesOwnFleet || !string.IsNullOrWhiteSpace(clientQueue);
}
