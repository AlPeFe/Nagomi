using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.Vehicles;

namespace Nagomi.IntegrationTests.Api;

public sealed class VehicleTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_vehicle_produces_public_id()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/vehicles",
            new UpsertVehicleCommand("Ambulancia 01", "AMB-01"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        vehicle.GetProperty("publicId").GetString().Should().StartWith("VHC-");
        vehicle.GetProperty("name").GetString().Should().Be("Ambulancia 01");
        vehicle.GetProperty("externalCode").GetString().Should().Be("AMB-01");
    }

    [Fact]
    public async Task Create_vehicle_requires_name()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/vehicles", new UpsertVehicleCommand("", null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_vehicles_returns_created()
    {
        await CreateVehicle("Taxi 05", "TX-05");
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/admin/vehicles");
        list.EnumerateArray().Should().Contain(x => x.GetProperty("name").GetString() == "Taxi 05");
    }

    [Fact]
    public async Task Delete_vehicle_soft_deletes()
    {
        var id = (await CreateVehicle("Silla 09", "CHR-09")).GetProperty("id").GetGuid();
        var response = await _client.DeleteAsync($"/api/admin/vehicles/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/admin/vehicles");
        list.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Vehicle_endpoints_require_admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "default");
        var response = await client.GetAsync("/api/admin/vehicles");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Coordination_requires_web_auth()
    {
        var response = await _client.GetAsync("/api/coordination");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        rows.EnumerateArray().Should().NotBeNull();
    }

    private async Task<JsonElement> CreateVehicle(string name, string? externalCode) =>
        await (await _client.PostAsJsonAsync("/api/admin/vehicles", new UpsertVehicleCommand(name, externalCode)))
            .Content.ReadFromJsonAsync<JsonElement>();
}
