using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.UnitTests.ReferenceData;

public sealed class HealthcareFacilityTests
{
    [Fact]
    public async Task Resolve_official_code_returns_official_facility_and_ignores_manual_matches()
    {
        var official = Facility(HealthcareFacilitySource.Official);
        var manual = Facility(HealthcareFacilitySource.Manual);
        var db = new TestNagomiDb();
        db.Facilities.Seed(official, manual);

        var result = await Invoke<Results<Ok<HealthcareFacilityResponse>, NotFound, ValidationProblem>>(
            "ResolveHealthcareFacility", " CCN-1 ", null, db, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<HealthcareFacilityResponse>>()
            .Which.Value!.PublicId.Should().Be(official.PublicId);
    }

    [Fact]
    public async Task Create_manual_facility_trims_optional_data_and_assigns_public_identifier()
    {
        var db = new TestNagomiDb();
        var request = new CreateManualHealthcareFacilityRequest(
            " Clinic ", " Main ", null, null, null, null, null, " ", " 28001 ", " 28079 ",
            " 28 ", " 13 ", " 555 ", " note ", 40.4m, -3.7m);

        var result = await Invoke<Results<Created<HealthcareFacilityResponse>, ValidationProblem>>(
            "CreateManualHealthcareFacility", request, db, CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<HealthcareFacilityResponse>>().Subject;
        created.Value!.Should().BeEquivalentTo(new
        {
            Name = "Clinic",
            Source = HealthcareFacilitySource.Manual,
            Phone = "555",
            Notes = "note"
        });
        created.Value.PublicId.Should().NotBeEmpty();
        db.Facilities.Should().ContainSingle();
        db.Facilities.Single().AdditionalDetails.Should().BeNull();
    }

    [Fact]
    public void Facility_snapshot_is_detached_and_retains_location_assumptions()
    {
        var facility = Facility(HealthcareFacilitySource.Official);
        facility.Street = "Old street";
        facility.Latitude = 40.4m;
        var snapshot = facility.Snapshot();

        facility.Name = "Renamed";
        facility.Street = "New street";
        facility.Latitude = 41m;

        snapshot.Name.Should().Be("Hospital");
        snapshot.Address.Street.Should().Be("Old street");
        snapshot.Latitude.Should().Be(40.4m);
        snapshot.Ccn.Should().Be("CCN-1");
    }

    private static HealthcareFacility Facility(HealthcareFacilitySource source) => new()
    {
        Name = "Hospital",
        Source = source,
        Ccn = "CCN-1",
        Codcnh = "CNH-1",
        IsActive = true
    };

    private static async Task<TResult> Invoke<TResult>(string name, params object?[] arguments)
    {
        var method = typeof(ReferenceDataEndpoints).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
        return await (Task<TResult>)method.Invoke(null, arguments)!;
    }
}
