using FluentAssertions;
using Nagomi.Api.Features.ProviderIntegration;
using Xunit;

namespace Nagomi.UnitTests.ProviderIntegration;

/// <summary>
/// Regla de publicación de un traslado adjudicado. Es la diferencia entre «no hay cola» (no es un
/// error: el traslado se consulta por la API) y «hay un destino al que avisar».
/// </summary>
public sealed class PublicationPolicyTests
{
    [Fact]
    public void Empresa_de_ambulancias_publica_en_su_flota_aunque_el_cliente_no_tenga_cola()
    {
        PublicationPolicy.ShouldPublish(tenantExecutesOwnFleet: true, clientQueue: null).Should().BeTrue();
        PublicationPolicy.ShouldPublish(true, "   ").Should().BeTrue();
        PublicationPolicy.ShouldPublish(true, "nagomi.cliente-acme").Should().BeTrue();
    }

    [Fact]
    public void Instalacion_publicadora_publica_en_la_cola_del_cliente()
    {
        PublicationPolicy.ShouldPublish(false, "nagomi.cliente-acme").Should().BeTrue();
    }

    [Fact]
    public void Cliente_sin_cola_no_publica_en_ninguna_cola()
    {
        // Sin destino no se avisa a nadie: el traslado sigue expuesto por la API con normalidad.
        PublicationPolicy.ShouldPublish(false, null).Should().BeFalse();
        PublicationPolicy.ShouldPublish(false, "").Should().BeFalse();
        PublicationPolicy.ShouldPublish(false, "  ").Should().BeFalse();
    }
}
