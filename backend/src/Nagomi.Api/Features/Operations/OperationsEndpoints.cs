using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Features.Operations;

public sealed record JourneyOperationsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    JourneyStatus? Status = null,
    Guid? ProviderId = null,
    string? ContractCode = null,
    JourneyDirection? Direction = null,
    string? ReasonCode = null,
    string? OriginMunicipalityCode = null,
    string? DestinationMunicipalityCode = null,
    string? RetrievalState = null,
    string? Search = null);

public sealed record JourneyOperationsRow(
    Guid JourneyId,
    string JourneyPublicId,
    Guid RequestId,
    string RequestPublicId,
    DateTimeOffset OperationalAt,
    bool PickupTimePending,
    string PatientName,
    string? PatientPhone,
    string Origin,
    string Destination,
    JourneyDirection Direction,
    string Reason,
    string Requirements,
    JourneyStatus Status,
    Guid? ProviderId,
    string? Provider,
    string? ContractCode,
    string? ProviderReference,
    string? RetrievalState,
    bool ExternallyModified,
    bool ProviderCancelled);

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/operations").RequireAuthorization(UserAuthorizationPolicies.Web).WithTags("Operations");
        group.MapGet("/journeys", List);
        group.MapGet("/journeys/export.csv", ExportCsv);
        group.MapGet("/requests", ListRequests);
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<JourneyOperationsRow>>> List(
        [AsParameters] JourneyOperationsQuery query, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var rows = await Build(query, db, clock, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<JourneyOperationsRow>>(rows);
    }

    private static async Task<FileContentHttpResult> ExportCsv(
        [AsParameters] JourneyOperationsQuery query, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var rows = await Build(query, db, clock, cancellationToken);
        var csv = new StringBuilder("Operational time,Pending,Patient,Phone,Origin,Destination,Direction,Reason,Requirements,Status,Provider,Contract,Request,Journey,Provider reference,Retrieval state\r\n");
        foreach (var row in rows)
        {
            csv.AppendJoin(',', new[]
            {
                Cell(row.OperationalAt.ToString("O", CultureInfo.InvariantCulture)), Cell(row.PickupTimePending ? "Yes" : "No"),
                Cell(row.PatientName), Cell(row.PatientPhone), Cell(row.Origin), Cell(row.Destination), Cell(row.Direction.ToString()),
                Cell(row.Reason), Cell(row.Requirements), Cell(row.Status.ToString()), Cell(row.Provider), Cell(row.ContractCode),
                Cell(row.RequestPublicId), Cell(row.JourneyPublicId), Cell(row.ProviderReference), Cell(row.RetrievalState)
            }).Append("\r\n");
        }
        return TypedResults.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"nagomi-journeys-{clock.GetUtcNow():yyyyMMddHHmmss}.csv");
    }

    private static async Task<Ok<IReadOnlyList<TransportRequestRecord>>> ListRequests(
        TransportRequestStatus? status, string? search, ITransportDb db, CancellationToken cancellationToken)
    {
        var requests = db.TransportRequests.AsNoTracking();
        if (status.HasValue) requests = requests.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            requests = requests.Where(x => (x.PublicId != null && x.PublicId.ToLower().Contains(term)) ||
                (x.Patient != null && (((x.Patient.FirstName ?? "") + " " + (x.Patient.LastName ?? "")).ToLower().Contains(term) ||
                 (x.Patient.DocumentNumber != null && x.Patient.DocumentNumber.ToLower().Contains(term)) ||
                 (x.Patient.Phone != null && x.Patient.Phone.ToLower().Contains(term)))));
        }
        var result = await requests.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TransportRequestRecord>>(result);
    }

    private static async Task<IReadOnlyList<JourneyOperationsRow>> Build(
        JourneyOperationsQuery filter, ITransportDb db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var from = filter.From ?? today.AddDays(-1);
        var to = filter.To ?? today.AddDays(1);
        var requests = db.TransportRequests.AsNoTracking();
        var journeys = db.Journeys.AsNoTracking().Where(x => x.ServiceDate >= from && x.ServiceDate <= to);
        if (!filter.Status.HasValue && filter.From is null && filter.To is null)
            journeys = journeys.Where(x => x.CurrentStatus != JourneyStatus.Completed && x.CurrentStatus != JourneyStatus.Cancelled);
        if (filter.Status.HasValue) journeys = journeys.Where(x => x.CurrentStatus == filter.Status);
        if (filter.Direction.HasValue) journeys = journeys.Where(x => x.Direction == filter.Direction);
        if (!string.IsNullOrWhiteSpace(filter.OriginMunicipalityCode))
            journeys = journeys.Where(x => x.Origin.MunicipalityCode == filter.OriginMunicipalityCode.Trim());
        if (!string.IsNullOrWhiteSpace(filter.DestinationMunicipalityCode))
            journeys = journeys.Where(x => x.Destination.MunicipalityCode == filter.DestinationMunicipalityCode.Trim());
        if (!string.IsNullOrWhiteSpace(filter.RetrievalState))
            journeys = journeys.Where(x => x.RetrievalState == filter.RetrievalState.Trim());

        var joined = journeys.Join(requests, j => j.TransportRequestId, r => r.Id, (j, r) => new { j, r });
        if (filter.ProviderId.HasValue) joined = joined.Where(x => x.r.ProviderId == filter.ProviderId);
        if (!string.IsNullOrWhiteSpace(filter.ContractCode)) joined = joined.Where(x => x.r.ContractCode == filter.ContractCode.Trim());
        if (!string.IsNullOrWhiteSpace(filter.ReasonCode)) joined = joined.Where(x => x.r.Reason != null && x.r.Reason.Code == filter.ReasonCode.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            joined = joined.Where(x =>
                (x.r.PublicId != null && x.r.PublicId.ToLower().Contains(term)) || x.j.PublicId.ToLower().Contains(term) ||
                (x.j.ProviderReference != null && x.j.ProviderReference.ToLower().Contains(term)) ||
                (x.r.Patient != null && (((x.r.Patient.FirstName ?? "") + " " + (x.r.Patient.LastName ?? "")).ToLower().Contains(term) ||
                 (x.r.Patient.DocumentNumber != null && x.r.Patient.DocumentNumber.ToLower().Contains(term)) ||
                 (x.r.Patient.Phone != null && x.r.Patient.Phone.ToLower().Contains(term)))));
        }

        var values = await joined.ToListAsync(cancellationToken);
        return values.Select(x => new JourneyOperationsRow(
            x.j.Id, x.j.PublicId, x.r.Id, x.r.PublicId!,
            x.j.Direction == JourneyDirection.Return ? x.j.Schedule.ScheduledPickupAt!.Value : x.j.Schedule.ScheduledStartAt,
            x.j.Schedule.PickupTimePending,
            x.r.Patient == null ? "" : ((x.r.Patient.FirstName ?? "") + " " + (x.r.Patient.LastName ?? "")).Trim(),
            x.r.Patient == null ? null : x.r.Patient.Phone,
            x.j.Origin.Name ?? x.j.Origin.Street ?? "", x.j.Destination.Name ?? x.j.Destination.Street ?? "",
            x.j.Direction, x.r.Reason == null ? "" : x.r.Reason.Description, x.j.Requirements.Mobility.ToString(),
            x.j.CurrentStatus, x.r.ProviderId, x.r.ProviderName, x.r.ContractCode, x.j.ProviderReference, x.j.RetrievalState,
            x.j.ExternallyModified,
            x.j.CurrentStatus == JourneyStatus.Cancelled && x.j.CurrentCancellingParty == CancellingParty.TransportProvider))
            .OrderBy(x => x.OperationalAt)
            .ToArray();
    }

    private static string Cell(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
}
