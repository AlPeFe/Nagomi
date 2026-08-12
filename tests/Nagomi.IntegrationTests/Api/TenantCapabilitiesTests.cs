using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.Tenant;

namespace Nagomi.IntegrationTests.Api;

public sealed class TenantCapabilitiesTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Capabilities_default_to_all_three_enabled()
    {
        await _client.PutAsJsonAsync("/api/admin/tenant/capabilities", new TenantSettingsCommand(true, true, true));
        var response = await _client.GetAsync("/api/admin/tenant/capabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caps = await response.Content.ReadFromJsonAsync<TenantSettingsResponse>();
        caps.Should().NotBeNull();
        caps!.PublishesRequests.Should().BeTrue();
        caps.ExecutesTransports.Should().BeTrue();
        caps.HandlesEmergencies.Should().BeTrue();
    }

    [Fact]
    public async Task Capabilities_can_be_updated_to_a_subset()
    {
        var update = new TenantSettingsCommand(false, true, false);
        var response = await _client.PutAsJsonAsync("/api/admin/tenant/capabilities", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caps = await response.Content.ReadFromJsonAsync<TenantSettingsResponse>();
        caps!.PublishesRequests.Should().BeFalse();
        caps.ExecutesTransports.Should().BeTrue();
        caps.HandlesEmergencies.Should().BeFalse();
    }

    [Fact]
    public async Task Capabilities_can_enable_multiple_simultaneously()
    {
        var update = new TenantSettingsCommand(true, true, false);
        var response = await _client.PutAsJsonAsync("/api/admin/tenant/capabilities", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caps = await response.Content.ReadFromJsonAsync<TenantSettingsResponse>();
        caps!.PublishesRequests.Should().BeTrue();
        caps.ExecutesTransports.Should().BeTrue();
        caps.HandlesEmergencies.Should().BeFalse();
    }

    [Fact]
    public async Task Create_client_without_contract_produces_public_id()
    {
        var command = new UpsertClientCommand("Mutua Pepe", "B12345678", "Juan", "600000000", "juan@mutuapepe.es", "Calle Mayor 1");
        var response = await _client.PostAsJsonAsync("/api/admin/tenant/clients", command);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var client = await response.Content.ReadFromJsonAsync<ClientResponse>();
        client!.PublicId.Should().StartWith("CLI-");
        client.Name.Should().Be("Mutua Pepe");
        client.TaxId.Should().Be("B12345678");
    }

    [Fact]
    public async Task List_clients_excludes_inactive_by_default()
    {
        var created = await CreateClient();
        var id = created.GetProperty("id").GetGuid();
        await _client.PutAsJsonAsync($"/api/admin/tenant/clients/{id}", new UpsertClientCommand(
            "Mutua Pepe", "B12345678", null, null, null, null, IsActive: false));

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/admin/tenant/clients");
        list.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Client_creation_requires_name()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/tenant/clients",
            new UpsertClientCommand("", null, null, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Capabilities_endpoints_require_admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "default");
        var response = await client.GetAsync("/api/admin/tenant/capabilities");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<JsonElement> CreateClient() =>
        await (await _client.PostAsJsonAsync("/api/admin/tenant/clients",
            new UpsertClientCommand("Mutua Pepe", "B12345678", "Juan", "600000000", "juan@mutuapepe.es", "Calle Mayor 1")))
            .Content.ReadFromJsonAsync<JsonElement>();
}
