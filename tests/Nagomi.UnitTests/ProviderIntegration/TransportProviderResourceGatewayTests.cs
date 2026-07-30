using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.TransportRequests;

namespace Nagomi.UnitTests.ProviderIntegration;

public sealed class TransportProviderResourceGatewayTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Retrieval_returns_current_snapshot_and_route()
    {
        var (gateway, db, request, journey, identity) = Fixture();

        var requestResult = await gateway.GetRequestAsync(request.PublicId!, default);
        var journeyResult = await gateway.GetJourneyAsync(journey.PublicId, default);

        requestResult.Should().BeEquivalentTo(new
        {
            identity.ProviderId,
            ContractCode = "CONTRACT-A",
            Snapshot = request
        });
        journeyResult.Should().BeEquivalentTo(new
        {
            identity.ProviderId,
            ContractCode = "CONTRACT-A",
            Snapshot = journey
        });
        (await gateway.GetJourneyAuthorizationAsync(journey.PublicId, default))
            .Should().BeEquivalentTo(new ProviderResourceAuthorization(identity.ProviderId, "CONTRACT-A"));
        db.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Request_snapshot_replaces_only_provider_permitted_fields()
    {
        var (gateway, db, request, _, identity) = Fixture();
        var patient = request.Patient;
        var reason = request.Reason;
        var recurrence = request.Recurrence;
        var payload = Json(new UpdateRequestCommand(new TransportRequestSnapshot(
            new PatientDetails("Changed"),
            new TransportReasonSnapshot("OTHER", "Changed"),
            Facility("New origin"),
            Private("New destination"),
            new TransportRequirements(MobilityType.Wheelchair),
            "OTHER-CONTRACT",
            Guid.NewGuid(),
            "Other provider",
            " REF-2 ",
            "Changed private note",
            " Provider update "),
            PropagateToJourneys: true,
            OverwriteExceptions: true));

        var result = await gateway.ExecuteAsync(
            "request.replace", request.PublicId!, payload, identity, At(14), default);

        result.StatusCode.Should().Be(200, result.Body);
        request.Patient.Should().BeSameAs(patient);
        request.Reason.Should().BeSameAs(reason);
        request.Recurrence.Should().BeSameAs(recurrence);
        request.ContractCode.Should().Be("CONTRACT-A");
        request.ProviderId.Should().Be(identity.ProviderId);
        request.PrivateNotes.Should().Be("private");
        request.DefaultOrigin!.Name.Should().Be("New origin");
        request.ProviderReference.Should().Be("REF-2");
        request.ProviderVisibleNotes.Should().Be("Provider update");
        request.UpdatedAt.Should().Be(At(14));
        db.Audits.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            EntityType = "TransportRequest",
            EntityIdentifier = request.PublicId,
            Action = "Updated",
            Source = ChangeSource.TransportProvider,
            Actor = identity.ClientId,
            RecordedAt = At(14)
        });
    }

    [Fact]
    public async Task Journey_snapshot_is_external_and_becomes_recurrence_exception()
    {
        var (gateway, db, _, journey, identity) = Fixture();
        var command = new JourneySnapshotCommand(
            Private("Replacement origin"), Facility("Replacement destination"),
            new TransportRequirements(MobilityType.Stretcher),
            JourneySchedule.Outbound(At(16), true), " visible ", " ref ",
            ChangeSource.Nagomi, "untrusted-actor");

        var result = await gateway.ExecuteAsync(
            "journey.replace", journey.PublicId, Json(command), identity, At(13), default);

        result.StatusCode.Should().Be(200, result.Body);
        journey.Origin.Name.Should().Be("Replacement origin");
        journey.ProviderReference.Should().Be("ref");
        journey.IsRecurrenceException.Should().BeTrue();
        journey.ExternallyModified.Should().BeTrue();
        db.Audits.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Source = ChangeSource.TransportProvider,
            Actor = identity.ClientId
        });
    }

    [Fact]
    public async Task Status_ignores_supplied_source_and_actor_and_uses_acceptance_time()
    {
        var (gateway, _, _, journey, identity) = Fixture();
        var command = new AddJourneyStatusCommand(
            JourneyStatus.Activated, At(12), "status-1", ChangeSource.Nagomi, "spoofed", "AMB-4");

        var result = await gateway.ExecuteAsync(
            "journey.status", journey.PublicId, Json(command), identity, At(12, 5), default);

        result.StatusCode.Should().Be(200);
        journey.CurrentStatus.Should().Be(JourneyStatus.Activated);
        journey.ExternallyModified.Should().BeTrue();
        journey.StatusHistory.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Source = ChangeSource.TransportProvider,
            Actor = identity.ClientId,
            RecordedAt = At(12, 5),
            ExternalResourceCode = "AMB-4"
        });
    }

    [Fact]
    public async Task Exceptional_journey_is_added_as_external_whole_journey_exception()
    {
        var (gateway, _, request, _, identity) = Fixture();
        var payload = Json(new
        {
            direction = JourneyDirection.Return,
            serviceDate = new DateOnly(2026, 8, 2),
            origin = Facility("Hospital"),
            destination = Private("Home"),
            requirements = new TransportRequirements(),
            schedule = JourneySchedule.Return(At(18)),
            providerVisibleNotes = "added",
            providerReference = "EX-1"
        });

        var result = await gateway.ExecuteAsync(
            "request.journey.add", request.PublicId!, payload, identity, At(15), default);

        result.StatusCode.Should().Be(201, result.Body);
        request.JourneyRecords.Should().HaveCount(2);
        request.JourneyRecords.Single(x => x.Direction == JourneyDirection.Return)
            .Should().BeEquivalentTo(new
            {
                IsManuallyAdded = true,
                IsRecurrenceException = true,
                ExternallyModified = true,
                ProviderReference = "EX-1"
            });
    }

    [Fact]
    public async Task Cancellation_is_external_and_unauthorized_execution_is_rejected()
    {
        var (gateway, db, _, journey, identity) = Fixture();
        var command = new CancelCommand(
            CancellationReason.ProviderUnavailable, CancellingParty.TransportProvider,
            Source: ChangeSource.Nagomi, Actor: "spoofed", IdempotencyKey: "cancel-1");
        var outsider = identity with { ProviderId = Guid.NewGuid() };

        var denied = await gateway.ExecuteAsync(
            "journey.cancel", journey.PublicId, Json(command), outsider, At(11), default);
        var accepted = await gateway.ExecuteAsync(
            "journey.cancel", journey.PublicId, Json(command), identity, At(11), default);

        denied.StatusCode.Should().Be(403);
        accepted.StatusCode.Should().Be(200);
        journey.CurrentStatus.Should().Be(JourneyStatus.Cancelled);
        journey.ExternallyModified.Should().BeTrue();
        journey.StatusHistory.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Source = ChangeSource.TransportProvider,
            Actor = identity.ClientId,
            RecordedAt = At(11)
        });
        db.SaveCount.Should().Be(1);
    }

    private static (TransportProviderResourceGateway Gateway, TestTransportDb Db,
        TransportRequestRecord Request, JourneyRecord Journey, ProviderIdentity Identity) Fixture()
    {
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var journey = new JourneyRecord
        {
            TransportRequestId = requestId,
            PublicId = "JRN-1",
            Direction = JourneyDirection.Outbound,
            ServiceDate = new DateOnly(2026, 8, 1),
            Origin = Private("Home"),
            Destination = Facility("Hospital"),
            Schedule = JourneySchedule.Outbound(At(12), true)
        };
        var recurrence = new RecurrencePattern(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 8),
            [new WeekdaySchedule(DayOfWeek.Saturday, new TimeOnly(12, 0))]);
        var request = new TransportRequestRecord
        {
            Id = requestId,
            PublicId = "REQ-1",
            Status = TransportRequestStatus.Active,
            Patient = new PatientDetails("Patient", "One", "DOC", "HC", "600000000"),
            Reason = new TransportReasonSnapshot("R", "Reason"),
            DefaultOrigin = Private("Home"),
            DefaultDestination = Facility("Hospital"),
            ContractCode = "CONTRACT-A",
            ProviderId = providerId,
            ProviderName = "Provider",
            PrivateNotes = "private",
            Recurrence = recurrence,
            JourneyRecords = [journey]
        };
        var db = new TestTransportDb([request]);
        var identity = new ProviderIdentity(providerId, "provider-client", new HashSet<string> { "CONTRACT-A" });
        return (new TransportProviderResourceGateway(db), db, request, journey, identity);
    }

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value, JsonOptions);
    private static LocationSnapshot Private(string name) => new(LocationType.PrivateAddress, name: name);
    private static LocationSnapshot Facility(string name) => new(LocationType.HealthcareFacility, name: name);
    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 8, 1, hour, minute, 0, TimeSpan.Zero);

    private sealed class TestTransportDb(IEnumerable<TransportRequestRecord> requests) : ITransportDb
    {
        private readonly List<TransportRequestRecord> _requests = requests.ToList();
        internal List<TransportAuditRecord> Audits { get; } = [];
        internal int SaveCount { get; private set; }
        public IQueryable<TransportRequestRecord> TransportRequests => new AsyncEnumerable<TransportRequestRecord>(_requests);
        public IQueryable<JourneyRecord> Journeys => new AsyncEnumerable<JourneyRecord>(_requests.SelectMany(x => x.JourneyRecords));
        public IQueryable<TransportAuditRecord> TransportAudit => new AsyncEnumerable<TransportAuditRecord>(Audits);
        public void Add(TransportRequestRecord request) => _requests.Add(request);
        public void Add(JourneyRecord journey)
        {
            if (_requests.SelectMany(x => x.JourneyRecords).All(x => x.Id != journey.Id))
                _requests.Single(x => x.Id == journey.TransportRequestId).JourneyRecords.Add(journey);
        }
        public void Add(JourneyStatusRecord status) { }
        public void Add(TransportAuditRecord audit) => Audits.Add(audit);
        public void Remove(TransportRequestRecord request) => _requests.Remove(request);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class AsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public AsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public AsyncEnumerable(Expression expression) : base(expression) { }
        IQueryProvider IQueryable.Provider => new AsyncQueryProvider<T>(this);
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new AsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());
    }

    private sealed class AsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) => new AsyncEnumerable<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var result = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
                .MakeGenericMethod(resultType).Invoke(inner, [expression]);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [result])!;
        }
    }

    private sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
    }
}
