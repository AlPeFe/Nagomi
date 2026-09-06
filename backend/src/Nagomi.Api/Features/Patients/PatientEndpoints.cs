using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.PublicIds;

namespace Nagomi.Api.Features.Patients;

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/patients")
            .RequireAuthorization(UserAuthorizationPolicies.Admin)
            .WithTags("Patients");
        admin.MapGet("/", ListAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{id:guid}", UpdateAsync);
        admin.MapDelete("/{id:guid}", DeleteAsync);

        // Web (any authenticated user): search used by the request form autocomplete,
        // and the idempotent "ensure" the form calls on submit so the directory fills itself.
        var web = endpoints.MapGroup("/api/patients")
            .RequireAuthorization(UserAuthorizationPolicies.Web)
            .WithTags("Patients");
        web.MapGet("/search", SearchAsync);
        web.MapPost("/ensure", EnsureAsync);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<PatientResponse>>> ListAsync(
        ITransportDb db, CancellationToken cancellationToken, string? search = null, bool includeInactive = false)
    {
        var query = db.Patients.AsNoTracking();
        if (!includeInactive)
            query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                (x.FirstName != null && x.FirstName.Contains(term)) ||
                (x.LastName != null && x.LastName.Contains(term)) ||
                (x.DocumentNumber != null && x.DocumentNumber.Contains(term.ToUpperInvariant())));
        }
        var patients = await query
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new PatientResponse(x.Id, x.PublicId, x.FirstName, x.LastName,
                x.DocumentNumber, x.HealthCardNumber, x.Phone, x.Notes, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<PatientResponse>>(patients);
    }

    private static async Task<Ok<IReadOnlyList<PatientResponse>>> SearchAsync(
        ITransportDb db, CancellationToken cancellationToken, string? q = null, int limit = 10)
    {
        var term = q?.Trim() ?? "";
        var take = Math.Clamp(limit is 0 ? 10 : limit, 1, 50);
        if (term.Length == 0)
            return TypedResults.Ok<IReadOnlyList<PatientResponse>>([]);

        var norm = term.ToUpperInvariant();
        var patients = await db.Patients.AsNoTracking()
            .Where(x => x.IsActive && (
                (x.FirstName != null && x.FirstName.Contains(term)) ||
                (x.LastName != null && x.LastName.Contains(term)) ||
                (x.DocumentNumber != null && x.DocumentNumber.Contains(norm))))
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Take(take)
            .Select(x => new PatientResponse(x.Id, x.PublicId, x.FirstName, x.LastName,
                x.DocumentNumber, x.HealthCardNumber, x.Phone, x.Notes, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<PatientResponse>>(patients);
    }

    private static async Task<Results<Created<PatientResponse>, ValidationProblem>> CreateAsync(
        UpsertPatientCommand command, ITransportDb db, IPublicIdGenerator ids,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!HasIdentity(command))
            return ValidationProblem("El paciente necesita un nombre o un documento de identidad.");

        var document = PatientMapping.Clean(command.DocumentNumber)?.ToUpperInvariant();
        if (document != null && await db.Patients.AnyAsync(
                x => x.DocumentNumber == document, cancellationToken))
            return ValidationProblem($"Ya existe un paciente con el documento '{document}'.");

        var patient = new Patient
        {
            PublicId = await ids.NextAsync("PAT", cancellationToken),
            FirstName = PatientMapping.Clean(command.FirstName),
            LastName = PatientMapping.Clean(command.LastName),
            DocumentNumber = document,
            HealthCardNumber = PatientMapping.Clean(command.HealthCardNumber),
            Phone = PatientMapping.Clean(command.Phone),
            Notes = PatientMapping.Clean(command.Notes),
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };
        db.Add(patient);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/patients/{patient.Id}", patient.ToResponse());
    }

    private static async Task<Results<Ok<PatientResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpsertPatientCommand command, ITransportDb db,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (patient is null) return TypedResults.NotFound();
        if (!HasIdentity(command))
            return ValidationProblem("El paciente necesita un nombre o un documento de identidad.");

        var document = PatientMapping.Clean(command.DocumentNumber)?.ToUpperInvariant();
        if (document != null && await db.Patients.AnyAsync(
                x => x.DocumentNumber == document && x.Id != patient.Id, cancellationToken))
            return ValidationProblem($"Ya existe un paciente con el documento '{document}'.");

        patient.FirstName = PatientMapping.Clean(command.FirstName);
        patient.LastName = PatientMapping.Clean(command.LastName);
        patient.DocumentNumber = document;
        patient.HealthCardNumber = PatientMapping.Clean(command.HealthCardNumber);
        patient.Phone = PatientMapping.Clean(command.Phone);
        patient.Notes = PatientMapping.Clean(command.Notes);
        patient.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(patient.ToResponse());
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ITransportDb db, CancellationToken cancellationToken)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (patient is null) return TypedResults.NotFound();
        // Soft-delete: historical requests keep their snapshot; the directory just stops suggesting it.
        patient.IsActive = false;
        patient.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Idempotent lookup-or-create used by the request form on submit: finds an active patient by
    /// document number, then by exact full name, and only creates one when nothing matches.
    /// </summary>
    private static async Task<Results<Ok<PatientResponse>, ValidationProblem>> EnsureAsync(
        UpsertPatientCommand command, ITransportDb db, IPublicIdGenerator ids,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!HasIdentity(command))
            return ValidationProblem("El paciente necesita un nombre o un documento de identidad.");

        var document = PatientMapping.Clean(command.DocumentNumber)?.ToUpperInvariant();
        if (document != null)
        {
            var byDocument = await db.Patients.AsNoTracking().SingleOrDefaultAsync(
                x => x.DocumentNumber == document && x.IsActive, cancellationToken);
            if (byDocument != null)
            {
                byDocument.Phone = PatientMapping.Clean(command.Phone) ?? byDocument.Phone;
                byDocument.HealthCardNumber = PatientMapping.Clean(command.HealthCardNumber) ?? byDocument.HealthCardNumber;
                byDocument.UpdatedAt = clock.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(byDocument.ToResponse());
            }
        }

        var firstName = PatientMapping.Clean(command.FirstName);
        var lastName = PatientMapping.Clean(command.LastName);
        if (firstName != null || lastName != null)
        {
            var byName = await db.Patients.AsNoTracking().SingleOrDefaultAsync(
                x => x.IsActive && x.FirstName == firstName && x.LastName == lastName, cancellationToken);
            if (byName != null)
            {
                byName.DocumentNumber ??= document;
                byName.Phone = PatientMapping.Clean(command.Phone) ?? byName.Phone;
                byName.UpdatedAt = clock.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(byName.ToResponse());
            }
        }

        // Create inline (Ensure always answers 200 Ok, unlike the admin POST which answers 201 Created).
        if (!HasIdentity(command))
            return ValidationProblem("El paciente necesita un nombre o un documento de identidad.");

        if (document != null && await db.Patients.AnyAsync(
                x => x.DocumentNumber == document, cancellationToken))
            return ValidationProblem($"Ya existe un paciente con el documento '{document}'.");

        var patient = new Patient
        {
            PublicId = await ids.NextAsync("PAT", cancellationToken),
            FirstName = firstName,
            LastName = lastName,
            DocumentNumber = document,
            HealthCardNumber = PatientMapping.Clean(command.HealthCardNumber),
            Phone = PatientMapping.Clean(command.Phone),
            Notes = PatientMapping.Clean(command.Notes),
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };
        db.Add(patient);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(patient.ToResponse());
    }

    private static bool HasIdentity(UpsertPatientCommand command) =>
        !string.IsNullOrWhiteSpace(command.FirstName) ||
        !string.IsNullOrWhiteSpace(command.LastName) ||
        !string.IsNullOrWhiteSpace(command.DocumentNumber);

    private static ValidationProblem ValidationProblem(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["patient"] = [message] });
}
