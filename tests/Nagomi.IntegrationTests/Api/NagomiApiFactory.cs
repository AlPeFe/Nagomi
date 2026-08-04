using System.Collections;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nagomi.Api.Features.EmergencyTransports;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.ProviderIntegration;

namespace Nagomi.IntegrationTests.Api;

public sealed class NagomiApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<ITransportDb>();
            services.RemoveAll<INagomiDb>();
            services.RemoveAll<IProviderIntegrationDb>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IProviderOutbox>();
            services.AddSingleton<FakeTransportDb>();
            services.AddSingleton<ITransportDb>(provider => provider.GetRequiredService<FakeTransportDb>());
            services.AddSingleton<INagomiDb, FakeReferenceDb>();
            services.AddSingleton<IProviderIntegrationDb, FakeProviderIntegrationDb>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero)));
            services.AddSingleton<IProviderOutbox, NoOpProviderOutbox>();
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JourneyScheduleJsonConverter()));
        });
    }
}

internal sealed class NoOpProviderOutbox : IProviderOutbox
{
    public Task<ProviderNotification?> AddAsync(
        string contractCode, string messageType, IntegrationEntityType entityType, string entityPublicId,
        string retrievalPath, Guid correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ProviderNotification?>(null);
}

internal sealed class JourneyScheduleJsonConverter : JsonConverter<JourneySchedule>
{
    public override JourneySchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var appointment = GetNullableDate(root, "appointmentAt");
        var start = root.GetProperty("scheduledStartAt").GetDateTimeOffset();
        var pickup = GetNullableDate(root, "scheduledPickupAt");
        var pending = root.TryGetProperty("pickupTimePending", out var value) && value.GetBoolean();
        return pickup.HasValue && !appointment.HasValue
            ? JourneySchedule.Return(pickup.Value, pending)
            : JourneySchedule.Outbound(appointment, appointment.HasValue, start, pickup);
    }

    public override void Write(Utf8JsonWriter writer, JourneySchedule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.AppointmentAt.HasValue) writer.WriteString("appointmentAt", value.AppointmentAt.Value);
        else writer.WriteNull("appointmentAt");
        writer.WriteString("scheduledStartAt", value.ScheduledStartAt);
        if (value.ScheduledPickupAt.HasValue) writer.WriteString("scheduledPickupAt", value.ScheduledPickupAt.Value);
        else writer.WriteNull("scheduledPickupAt");
        writer.WriteBoolean("pickupTimePending", value.PickupTimePending);
        writer.WriteEndObject();
    }

    private static DateTimeOffset? GetNullableDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDateTimeOffset()
            : null;
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeTransportDb : ITransportDb
{
    private readonly List<TransportRequestRecord> _requests = [];
    private readonly List<TransportAuditRecord> _audit = [];
    private readonly List<EmergencyTransportRecord> _emergencies = [];

    public IQueryable<TransportRequestRecord> TransportRequests => _requests.AsAsyncQueryable();
    public IQueryable<JourneyRecord> Journeys => _requests.SelectMany(x => x.JourneyRecords).AsAsyncQueryable();
    public IQueryable<TransportAuditRecord> TransportAudit => _audit.AsAsyncQueryable();
    public IQueryable<EmergencyTransportRecord> EmergencyTransports => _emergencies.AsAsyncQueryable();

    public void Add(TransportRequestRecord request) => _requests.Add(request);
    public void Add(JourneyRecord journey)
    {
        if (_requests.SelectMany(x => x.JourneyRecords).All(x => x.Id != journey.Id))
            _requests.Single(x => x.Id == journey.TransportRequestId).JourneyRecords.Add(journey);
    }
    public void Add(JourneyStatusRecord status) { }
    public void Add(TransportAuditRecord audit) => _audit.Add(audit);
    public void Add(EmergencyTransportRecord emergency) => _emergencies.Add(emergency);
    public void Remove(TransportRequestRecord request) => _requests.Remove(request);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeReferenceDb : INagomiDb
{
    public DbSet<IneAutonomousCommunity> IneAutonomousCommunities { get; } = new FakeDbSet<IneAutonomousCommunity>();
    public DbSet<IneProvince> IneProvinces { get; } = new FakeDbSet<IneProvince>();
    public DbSet<IneMunicipality> IneMunicipalities { get; } = new FakeDbSet<IneMunicipality>();
    public DbSet<TransportReason> TransportReasons { get; } = new FakeDbSet<TransportReason>();
    public DbSet<HealthcareFacility> HealthcareFacilities { get; } = new FakeDbSet<HealthcareFacility>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeProviderIntegrationDb : IProviderIntegrationDb
{
    public DbSet<TransportProvider> TransportProviders { get; } = new FakeDbSet<TransportProvider>();
    public DbSet<TransportContract> TransportContracts { get; } = new FakeDbSet<TransportContract>();
    public DbSet<ProviderContractRoute> ProviderContractRoutes { get; } = new FakeDbSet<ProviderContractRoute>();
    public DbSet<ProviderNotification> ProviderNotifications { get; } = new FakeDbSet<ProviderNotification>();
    public DbSet<ProviderCommandReceipt> ProviderCommandReceipts { get; } = new FakeDbSet<ProviderCommandReceipt>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeDbSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
{
    private readonly List<T> _items = [];

    public override IEntityType EntityType => null!;

    public override EntityEntry<T> Add(T entity)
    {
        _items.Add(entity);
        return null!;
    }

    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => _items.AsQueryable().Expression;
    IQueryProvider IQueryable.Provider => new AsyncQueryProvider<T>(_items.AsQueryable().Provider);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) =>
        new AsyncEnumerator<T>(_items.GetEnumerator());
}

internal static class AsyncQueryExtensions
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> values) => new AsyncEnumerable<T>(values);
}

internal sealed class AsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new AsyncEnumerable<T>(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncEnumerable<TElement>(expression);
    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var result = typeof(IQueryProvider).GetMethods()
            .Single(x => x.Name == nameof(IQueryProvider.Execute) && x.IsGenericMethod)
            .MakeGenericMethod(resultType)
            .Invoke(inner, [expression]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal sealed class AsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public AsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public AsyncEnumerable(Expression expression) : base(expression) { }
    IQueryProvider IQueryable.Provider => new AsyncQueryProvider<T>(this);
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new AsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());
}

internal sealed class AsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
