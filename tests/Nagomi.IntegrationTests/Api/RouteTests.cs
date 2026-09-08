using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.Routes;

namespace Nagomi.IntegrationTests.Api;

public sealed class RouteTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Route_endpoints_require_auth()
    {
        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        var response = await anonymous.GetAsync("/api/routes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_route_groups_two_journeys()
    {
        var (j1, j2) = await CreateTwoActiveJourneys();

        var response = await _client.PostAsJsonAsync("/api/routes", new UpsertRouteCommand(
            DateOnly.FromDateTime(DateTime.UtcNow), [j1, j2]));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var route = await response.Content.ReadFromJsonAsync<JsonElement>();
        route.GetProperty("publicId").GetString().Should().StartWith("RUT-");
        route.GetProperty("stops").GetArrayLength().Should().Be(2);
        // Order follows the journey id order.
        route.GetProperty("stops")[0].GetProperty("journeyId").GetGuid().Should().Be(j1);
        route.GetProperty("stops")[1].GetProperty("journeyId").GetGuid().Should().Be(j2);
    }

    [Fact]
    public async Task Create_route_requires_two_or_more_journeys()
    {
        var (j1, _) = await CreateTwoActiveJourneys();
        var single = await _client.PostAsJsonAsync("/api/routes", new UpsertRouteCommand(
            DateOnly.FromDateTime(DateTime.UtcNow), [j1]));
        single.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_routes_by_date_and_complete_route()
    {
        var (j1, j2) = await CreateTwoActiveJourneys();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await (await _client.PostAsJsonAsync("/api/routes", new UpsertRouteCommand(today, [j1, j2])))
            .Content.ReadFromJsonAsync<JsonElement>();
        var routeId = created.GetProperty("id").GetGuid();

        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/routes?date={today:yyyy-MM-dd}");
        list.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == routeId);

        var complete = await _client.PostAsync($"/api/routes/{routeId}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await complete.Content.ReadFromJsonAsync<JsonElement>();
        completed.GetProperty("status").GetInt32().Should().Be((int)RouteStatus.Completed);
    }

    /// <summary>Creates two active journeys (same date) by submitting two one-off requests, returning their journey ids.</summary>
    private async Task<(Guid J1, Guid J2)> CreateTwoActiveJourneys()
    {
        var appointment = DateTimeOffset.UtcNow.AddHours(2);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var j1 = await CreateJourney(date, appointment);
        var j2 = await CreateJourney(date, appointment.AddHours(1));
        return (j1, j2);
    }

    private async Task<Guid> CreateJourney(DateOnly serviceDate, DateTimeOffset appointment)
    {
        var snapshot = new Nagomi.Api.Features.TransportRequests.TransportRequestSnapshot(
            new Nagomi.Api.Domain.PatientDetails("Ruta", "Colectivo"),
            new Nagomi.Api.Domain.TransportReasonSnapshot("CONSULT", "Consulta externa"),
            new Nagomi.Api.Domain.LocationSnapshot(Nagomi.Api.Domain.LocationType.HealthcareFacility, "Hospital A"),
            new Nagomi.Api.Domain.LocationSnapshot(Nagomi.Api.Domain.LocationType.HealthcareFacility, "Hospital B"),
            new Nagomi.Api.Domain.TransportRequirements(), "CONTRACT-1", null, null, null, null, null, "nota", null);
        var draft = await _client.PostAsJsonAsync("/api/transport-requests/drafts", snapshot);
        draft.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = await draft.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = request.GetProperty("id").GetGuid();

        var submit = await _client.PostAsJsonAsync($"/api/transport-requests/{requestId}/submit/one-off",
            new Nagomi.Api.Features.TransportRequests.SubmitOneOffCommand(
                Nagomi.Api.Domain.JourneySchedule.Outbound(appointment, true), null));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/transport-requests/{requestId}");
        var journey = detail.GetProperty("journeyRecords").EnumerateArray().First();
        return journey.GetProperty("id").GetGuid();
    }
}
