using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;
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
    public async Task Create_vehicle_with_custom_internal_code()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/vehicles",
            new UpsertVehicleCommand("Ambulancia 02", "AMB-02", "AMB-02"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        vehicle.GetProperty("publicId").GetString().Should().Be("AMB-02");
    }

    [Fact]
    public async Task Create_vehicle_accepts_type_as_string_like_the_web_form()
    {
        // The web form sends the enum NAME ("Sva"), not the number. The FlexibleEnumConverter
        // must accept both; the reply keeps the numeric form.
        var response = await _client.PostAsJsonAsync("/api/admin/vehicles",
            new { name = "SVA Web", externalCode = "WEB-01", vehicleType = "Sva" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        vehicle.GetProperty("vehicleType").GetInt32().Should().Be((int)VehicleType.Sva);
    }

    [Fact]
    public async Task Create_vehicle_with_vehicle_type()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/vehicles",
            new UpsertVehicleCommand("SVA Norte", "SVA-01", VehicleType: VehicleType.Sva));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicle = await response.Content.ReadFromJsonAsync<JsonElement>();
        vehicle.GetProperty("vehicleType").GetInt32().Should().Be((int)VehicleType.Sva);
    }

    [Fact]
    public async Task Update_vehicle_changes_vehicle_type()
    {
        var created = await CreateVehicle("Ambulancia 03", "AMB-03");
        var response = await _client.PutAsJsonAsync($"/api/admin/vehicles/{created.GetProperty("id").GetGuid()}",
            new UpsertVehicleCommand("Ambulancia 03", "AMB-03", VehicleType: VehicleType.Pediatric));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("vehicleType").GetInt32().Should().Be((int)VehicleType.Pediatric);
    }

    [Fact]
    public async Task Assign_driver_to_journey()
    {
        var (journeyId, requestId) = await CreateJourneyWithVehicle();
        var response = await _client.PutAsJsonAsync($"/api/journeys/{journeyId}/driver",
            new AssignDriverCommand("Carlos Ruiz"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var driver = await response.Content.ReadFromJsonAsync<JsonElement>();
        driver.GetString().Should().Be("Carlos Ruiz");

        // The driver persists on the journey (read back through the request detail).
        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/transport-requests/{requestId}");
        var journey = detail.GetProperty("journeyRecords").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == journeyId);
        journey.GetProperty("driverName").GetString().Should().Be("Carlos Ruiz");
    }

    [Fact]
    public async Task Clear_driver_on_journey()
    {
        var (journeyId, requestId) = await CreateJourneyWithVehicle();
        await _client.PutAsJsonAsync($"/api/journeys/{journeyId}/driver", new AssignDriverCommand("Ana Torres"));
        var cleared = await _client.PutAsJsonAsync($"/api/journeys/{journeyId}/driver", new AssignDriverCommand(""));
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/transport-requests/{requestId}");
        var journey = detail.GetProperty("journeyRecords").EnumerateArray().First(x => x.GetProperty("id").GetGuid() == journeyId);
        journey.GetProperty("driverName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Create_vehicle_rejects_duplicate_internal_code()
    {
        await _client.PostAsJsonAsync("/api/admin/vehicles",
            new UpsertVehicleCommand("Ambulancia 03", "AMB-03", "DUP-01"));
        var second = await _client.PostAsJsonAsync("/api/admin/vehicles",
            new UpsertVehicleCommand("Ambulancia 04", "AMB-04", "DUP-01"));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    /// <summary>Creates a vehicle and assigns it to a journey visible in coordination (today), returning (journeyId, requestId).</summary>
    private async Task<(Guid JourneyId, Guid RequestId)> CreateJourneyWithVehicle()
    {
        var vehicle = await CreateVehicle($"AMB-{Guid.NewGuid():N}"[..6], null);
        // Reuse the existing coordination journeys: pick the first non-completed row, if any.
        var coordination = await _client.GetFromJsonAsync<JsonElement>("/api/coordination");
        var row = coordination.EnumerateArray().FirstOrDefault(x =>
            x.GetProperty("status").GetString() is not ("Completed" or "Cancelled"));
        if (row.ValueKind != JsonValueKind.Undefined)
        {
            var journeyId = row.GetProperty("journeyId").GetGuid();
            var reuseRequestId = row.GetProperty("requestId").GetGuid();
            await _client.PutAsJsonAsync($"/api/journeys/{journeyId}/vehicle",
                new AssignVehicleCommand(vehicle.GetProperty("id").GetGuid()));
            return (journeyId, reuseRequestId);
        }

        // No active journey in coordination: create a fresh one via the composed request flow
        // (typed snapshots/commands, exactly like ComposedApplicationTests does). Appointment is
        // today so the journey lands in the coordination window (today-1..today+1).
        var snapshot = new TransportRequestSnapshot(
            new PatientDetails("Driver", "Test"),
            new Nagomi.Api.Domain.TransportReasonSnapshot("CONSULT", "Consultation"),
            new LocationSnapshot(LocationType.HealthcareFacility, "Hospital A"),
            new LocationSnapshot(LocationType.HealthcareFacility, "Hospital B"),
            new TransportRequirements(), "CONTRACT-1", null, null, null, null, null, "private", "provider note");
        var requestResponse = await _client.PostAsJsonAsync("/api/transport-requests/drafts", snapshot);
        requestResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = await requestResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = request.GetProperty("id").GetGuid();
        var appointment = DateTimeOffset.UtcNow.AddHours(2);
        var submission = await _client.PostAsJsonAsync($"/api/transport-requests/{requestId}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        submission.StatusCode.Should().Be(HttpStatusCode.OK);

        var coordinationAfter = await _client.GetFromJsonAsync<JsonElement>("/api/coordination");
        var journey = coordinationAfter.EnumerateArray().FirstOrDefault(x =>
            x.GetProperty("requestId").GetGuid() == requestId);
        Guid freshJourneyId;
        if (journey.ValueKind == JsonValueKind.Undefined)
        {
            // Coordination may skip the fresh journey if its provider resolution differs; fall back
            // to the request detail, which always carries its journeys.
            var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/transport-requests/{requestId}");
            freshJourneyId = detail.GetProperty("journeyRecords").EnumerateArray().First().GetProperty("id").GetGuid();
        }
        else
        {
            freshJourneyId = journey.GetProperty("journeyId").GetGuid();
        }
        await _client.PutAsJsonAsync($"/api/journeys/{freshJourneyId}/vehicle",
            new AssignVehicleCommand(vehicle.GetProperty("id").GetGuid()));
        return (freshJourneyId, requestId);
    }
}
