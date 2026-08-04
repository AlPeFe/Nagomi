using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.EmergencyTransports;

namespace Nagomi.IntegrationTests.Api;

public sealed class EmergencyTransportEndpointTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Emergency_transport_can_be_created_listed_and_fetched()
    {
        var created = await CreateEmergency();

        created.GetProperty("publicId").GetString().Should().StartWith("EMG-");
        created.GetProperty("status").GetInt32().Should().Be((int)EmergencyTransportStatus.Active);
        created.GetProperty("incident").GetProperty("latitude").GetDecimal().Should().Be(41.3874m);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/emergency-transports");
        list.EnumerateArray().Should().Contain(x =>
            x.GetProperty("publicId").GetString() == created.GetProperty("publicId").GetString());

        var id = created.GetProperty("id").GetGuid();
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"/api/emergency-transports/{id}");
        fetched.GetProperty("reason").GetString().Should().Be("Atropello en vía pública");
    }

    [Fact]
    public async Task Emergency_transport_can_be_cancelled_once()
    {
        var created = await CreateEmergency();
        var id = created.GetProperty("id").GetGuid();

        var cancelled = await _client.PostAsync($"/api/emergency-transports/{id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status")
            .GetInt32().Should().Be((int)EmergencyTransportStatus.Cancelled);

        (await _client.PostAsync($"/api/emergency-transports/{id}/cancel", null)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Emergency_transport_rejects_invalid_coordinates()
    {
        var body = new CreateEmergencyTransportCommand("Caída", new IncidentLocationSubmission(91, 2.1686m));

        var response = await _client.PostAsJsonAsync("/api/emergency-transports", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<JsonElement> CreateEmergency()
    {
        var body = new CreateEmergencyTransportCommand(
            "Atropello en vía pública",
            new IncidentLocationSubmission(41.3874m, 2.1686m, "Carrer de Balmes 1", "Barcelona"),
            "600111222",
            "Acceso por la entrada principal");
        var response = await _client.PostAsJsonAsync("/api/emergency-transports", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
