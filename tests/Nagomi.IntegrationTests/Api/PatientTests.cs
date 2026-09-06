using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nagomi.Api.Features.Patients;

namespace Nagomi.IntegrationTests.Api;

public sealed class PatientTests(NagomiApiFactory factory) : IClassFixture<NagomiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_patient_produces_public_id()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/patients",
            new UpsertPatientCommand("Ana", "García", "12345678A"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var patient = await response.Content.ReadFromJsonAsync<JsonElement>();
        patient.GetProperty("publicId").GetString().Should().StartWith("PAT-");
        patient.GetProperty("documentNumber").GetString().Should().Be("12345678A");
    }

    [Fact]
    public async Task Create_patient_requires_name_or_document()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/patients",
            new UpsertPatientCommand(null, null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_patient_rejects_duplicate_document()
    {
        await _client.PostAsJsonAsync("/api/admin/patients",
            new UpsertPatientCommand("Ana", "García", "11111111A"));
        var second = await _client.PostAsJsonAsync("/api/admin/patients",
            new UpsertPatientCommand("Otra", "Persona", "11111111A"));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_patients_returns_created_and_hides_inactive_by_default()
    {
        var created = await CreatePatient("Luis", "Pérez", "22222222B");
        await _client.DeleteAsync($"/api/admin/patients/{created.GetProperty("id").GetGuid()}");

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/admin/patients");
        list.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());

        var withInactive = await _client.GetFromJsonAsync<JsonElement>("/api/admin/patients?includeInactive=true");
        withInactive.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Search_finds_patient_by_name_or_document()
    {
        await CreatePatient("María", "López", "33333333C");
        var byName = await _client.GetFromJsonAsync<JsonElement>("/api/patients/search?q=María");
        byName.EnumerateArray().Should().Contain(x => x.GetProperty("lastName").GetString() == "López");
        var byDoc = await _client.GetFromJsonAsync<JsonElement>("/api/patients/search?q=33333333C");
        byDoc.EnumerateArray().Should().Contain(x => x.GetProperty("documentNumber").GetString() == "33333333C");
    }

    [Fact]
    public async Task Search_requires_web_auth_not_admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "default");
        var response = await client.GetAsync("/api/patients/search?q=María");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patient_admin_endpoints_require_admin()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "default");
        var response = await client.GetAsync("/api/admin/patients");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ensure_creates_then_reuses_by_document()
    {
        var first = await EnsurePatient("Pepe", "Sanz", "44444444D");
        var second = await EnsurePatient("Pepe", "Sanz", "44444444D");
        first.GetProperty("id").GetGuid().Should().Be(second.GetProperty("id").GetGuid());
        first.GetProperty("publicId").GetString().Should().Be(second.GetProperty("publicId").GetString());
    }

    [Fact]
    public async Task Update_patient_changes_fields_and_keeps_public_id()
    {
        var created = await CreatePatient("Antonio", "Ruiz", "55555555E");
        var response = await _client.PutAsJsonAsync($"/api/admin/patients/{created.GetProperty("id").GetGuid()}",
            new UpsertPatientCommand("Antonio", "Ruiz Molina", "55555555E", Phone: "600000000"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("lastName").GetString().Should().Be("Ruiz Molina");
        updated.GetProperty("phone").GetString().Should().Be("600000000");
        updated.GetProperty("publicId").GetString().Should().Be(created.GetProperty("publicId").GetString());
    }

    private async Task<JsonElement> CreatePatient(string firstName, string lastName, string? document = null) =>
        await (await _client.PostAsJsonAsync("/api/admin/patients",
                new UpsertPatientCommand(firstName, lastName, document)))
            .Content.ReadFromJsonAsync<JsonElement>();

    private async Task<JsonElement> EnsurePatient(string firstName, string lastName, string? document = null) =>
        await (await _client.PostAsJsonAsync("/api/patients/ensure",
                new UpsertPatientCommand(firstName, lastName, document)))
            .Content.ReadFromJsonAsync<JsonElement>();
}
