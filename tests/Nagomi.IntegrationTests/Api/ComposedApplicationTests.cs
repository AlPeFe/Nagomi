using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.IntegrationTests.Api;

public sealed class ComposedApplicationTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()
            .Should().Be("healthy");
    }

    [Fact]
    public async Task Draft_can_be_created_retrieved_and_deleted()
    {
        var created = await CreateDraft();
        var id = created.GetProperty("id").GetGuid();

        var retrieved = await _client.GetFromJsonAsync<JsonElement>($"/api/transport-requests/{id}");
        retrieved.GetProperty("patient").GetProperty("firstName").GetString().Should().Be("Ana");

        (await _client.DeleteAsync($"/api/transport-requests/{id}/draft")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/transport-requests/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task One_off_submission_is_listed_without_sensitive_patient_identifiers()
    {
        var draft = await CreateDraft();
        var id = draft.GetProperty("id").GetGuid();
        var appointment = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        var response = await _client.PostAsJsonAsync($"/api/transport-requests/{id}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await response.Content.ReadFromJsonAsync<JsonElement>();
        submitted.GetProperty("status").GetInt32().Should().Be((int)TransportRequestStatus.Active);

        var operationsResponse = await _client.GetAsync(
            "/api/operations/journeys?from=2026-08-01&to=2026-08-01");
        operationsResponse.EnsureSuccessStatusCode();
        var body = await operationsResponse.Content.ReadAsStringAsync();
        var rows = JsonDocument.Parse(body).RootElement;
        rows.GetArrayLength().Should().BeGreaterThan(0);
        rows.EnumerateArray().Should().Contain(x => x.GetProperty("requestId").GetGuid() == id);
        body.Should().NotContain("documentNumber").And.NotContain("healthCardNumber")
            .And.NotContain("DNI-SECRET").And.NotContain("CARD-SECRET");
    }

    [Fact]
    public async Task Request_without_publishable_contract_stays_active_and_unpublished()
    {
        var snapshot = new TransportRequestSnapshot(
            new PatientDetails("Ana", "Lopez", "DNI-SECRET", "CARD-SECRET", "600123123"),
            new Nagomi.Api.Domain.TransportReasonSnapshot("CONSULT", "Consultation"),
            new LocationSnapshot(LocationType.PrivateAddress, street: "Calle Mayor"),
            new LocationSnapshot(LocationType.HealthcareFacility, "Hospital Central"),
            new TransportRequirements(), null, null, null, null, "private", "provider note");
        var response = await _client.PostAsJsonAsync("/api/transport-requests/drafts", snapshot);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var appointment = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var submit = await _client.PostAsJsonAsync($"/api/transport-requests/{id}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submit.Content.ReadFromJsonAsync<JsonElement>();
        submitted.GetProperty("status").GetInt32().Should().Be((int)TransportRequestStatus.Active);
        var publicId = submitted.GetProperty("publicId").GetString();
        publicId.Should().NotBeNullOrWhiteSpace();

        var operationsResponse = await _client.GetAsync(
            "/api/operations/journeys?from=2026-08-01&to=2026-08-01");
        operationsResponse.EnsureSuccessStatusCode();
        var rows = JsonDocument.Parse(await operationsResponse.Content.ReadAsStringAsync()).RootElement;
        rows.EnumerateArray().Should().Contain(x => x.GetProperty("requestId").GetGuid() == id);
    }

    [Fact]
    public async Task Operational_csv_export_respects_filters_and_excludes_sensitive_identifiers()
    {
        var draft = await CreateDraft();
        var id = draft.GetProperty("id").GetGuid();
        var appointment = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var submit = await _client.PostAsJsonAsync($"/api/transport-requests/{id}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var csv = await _client.GetStringAsync("/api/operations/journeys/export.csv?from=2026-08-01&to=2026-08-01");
        csv.Should().Contain("Ana Lopez").And.Contain("Hospital Central")
            .And.Contain("Operational time,Pending");
        csv.Should().NotContain("DNI-SECRET").And.NotContain("CARD-SECRET")
            .And.NotContain("documentNumber").And.NotContain("healthCardNumber");
    }

    [Fact]
    public async Task Journey_statuses_are_idempotent_and_out_of_order_events_do_not_regress_current_status()
    {
        var draft = await CreateDraft();
        var requestId = draft.GetProperty("id").GetGuid();
        var appointment = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var submit = await _client.PostAsJsonAsync($"/api/transport-requests/{requestId}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        var submitted = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var journeyId = submitted.GetProperty("journeyRecords")[0].GetProperty("id").GetGuid();

        var activated = new AddJourneyStatusCommand(JourneyStatus.Activated, appointment.AddHours(-1),
            "status-1", ChangeSource.TransportProvider, "provider");
        var first = await PostStatus(journeyId, activated);
        var repeated = await PostStatus(journeyId, activated);
        repeated.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());

        await PostStatus(journeyId, new AddJourneyStatusCommand(JourneyStatus.ArrivedAtOrigin,
            appointment.AddHours(-2), "status-2", ChangeSource.TransportProvider, "provider"));

        var journey = await _client.GetFromJsonAsync<JsonElement>($"/api/journeys/{journeyId}");
        journey.GetProperty("currentStatus").GetInt32().Should().Be((int)JourneyStatus.Activated);
        var history = await _client.GetFromJsonAsync<JsonElement>($"/api/journeys/{journeyId}/statuses");
        history.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Manual_reference_facility_can_be_created_and_found()
    {
        var request = new CreateManualHealthcareFacilityRequest(
            "  Clinica Norte  ", "Mayor", "7", null, null, null, null, null, "28001",
            "28079", "28", "13", "+34 900 000 000", "manual", 40.42m, -3.70m);

        var response = await _client.PostAsJsonAsync("/api/reference-data/healthcare-facilities", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("name").GetString().Should().Be("Clinica Norte");
        created.GetProperty("source").GetInt32().Should().Be((int)HealthcareFacilitySource.Manual);

        var found = await _client.GetFromJsonAsync<JsonElement>(
            "/api/reference-data/healthcare-facilities?query=clinica");
        found.EnumerateArray().Should().Contain(x =>
            x.GetProperty("publicId").GetGuid() == created.GetProperty("publicId").GetGuid());
    }

    private async Task<JsonElement> CreateDraft()
    {
        var snapshot = new TransportRequestSnapshot(
            new PatientDetails("Ana", "Lopez", "DNI-SECRET", "CARD-SECRET", "600123123"),
            new Nagomi.Api.Domain.TransportReasonSnapshot("CONSULT", "Consultation"),
            new LocationSnapshot(LocationType.PrivateAddress, street: "Calle Mayor"),
            new LocationSnapshot(LocationType.HealthcareFacility, "Hospital Central"),
            new TransportRequirements(), "CONTRACT-1", null, null, null, "private", "provider note");
        var response = await _client.PostAsJsonAsync("/api/transport-requests/drafts", snapshot);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> PostStatus(Guid journeyId, AddJourneyStatusCommand command)
    {
        var response = await _client.PostAsJsonAsync($"/api/journeys/{journeyId}/statuses", command);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
