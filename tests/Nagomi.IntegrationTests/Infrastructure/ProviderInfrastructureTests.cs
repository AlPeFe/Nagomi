using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Persistence;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Nagomi.IntegrationTests.Infrastructure;

public sealed class ProviderInfrastructureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("nagomi_integration")
        .WithUsername("nagomi")
        .WithPassword("nagomi")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    public string PostgreSqlConnectionString => _postgres.GetConnectionString();
    public string RabbitMqConnectionString => _rabbit.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    public NagomiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NagomiDbContext>()
            .UseNpgsql(PostgreSqlConnectionString)
            .UseOpenIddict()
            .Options;
        return new NagomiDbContext(options);
    }
}

public sealed class ProviderInfrastructureTests(ProviderInfrastructureFixture fixture)
    : IClassFixture<ProviderInfrastructureFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrations_apply_cleanly_to_real_PostgreSql()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
        (await db.Database.CanConnectAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Rabbit_publisher_routes_minimal_messages_to_isolated_provider_queues()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var exchange = $"nagomi.test.{suffix}";
        var queueA = $"provider.a.{suffix}";
        var queueB = $"provider.b.{suffix}";
        var publisher = new RabbitMqProviderNotificationPublisher(Options.Create(new ProviderRabbitMqOptions
        {
            Uri = fixture.RabbitMqConnectionString,
            Exchange = exchange,
            DeadLetterExchange = $"{exchange}.dead"
        }));
        var first = Notification(queueA, "REQ-A", "DNI-SECRET", "CARD-SECRET");
        var second = Notification(queueB, "REQ-B", "OTHER-SECRET", "OTHER-CARD");

        await publisher.PublishAsync(first, queueA, CancellationToken.None);
        await publisher.PublishAsync(second, queueB, CancellationToken.None);

        var factory = new ConnectionFactory { Uri = new Uri(fixture.RabbitMqConnectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var fromA = await channel.BasicGetAsync(queueA, autoAck: true);
        var fromB = await channel.BasicGetAsync(queueB, autoAck: true);
        fromA.Should().NotBeNull();
        fromB.Should().NotBeNull();
        var bodyA = Encoding.UTF8.GetString(fromA!.Body.ToArray());
        var bodyB = Encoding.UTF8.GetString(fromB!.Body.ToArray());
        var messageA = JsonSerializer.Deserialize<ProviderNotificationMessage>(bodyA,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var messageB = JsonSerializer.Deserialize<ProviderNotificationMessage>(bodyB,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        messageA!.MessageId.Should().Be(first.MessageId);
        messageA.EntityPublicId.Should().Be("REQ-A");
        messageB!.MessageId.Should().Be(second.MessageId);
        messageB.EntityPublicId.Should().Be("REQ-B");
        bodyA.Should().NotContain("REQ-B").And.NotContain("DNI-SECRET").And.NotContain("CARD-SECRET");
        bodyB.Should().NotContain("REQ-A").And.NotContain("OTHER-SECRET").And.NotContain("OTHER-CARD");
    }

    [Fact]
    public async Task Broker_outage_records_failure_without_rolling_back_submitted_domain_data()
    {
        await EnsureMigrated();
        var seeded = await SeedRouteAndSubmission();
        var publisher = new RabbitMqProviderNotificationPublisher(Options.Create(new ProviderRabbitMqOptions
        {
            Uri = "amqp://guest:guest@127.0.0.1:1",
            RetryDelay = TimeSpan.FromMinutes(1),
            PollInterval = TimeSpan.FromHours(1)
        }));

        await RunWorkerUntil(async db =>
            await db.ProviderNotifications.AnyAsync(x => x.Id == seeded.NotificationId && x.FailedPublishAttempts == 1),
            publisher);

        await using var assertionDb = fixture.CreateDbContext();
        var request = await assertionDb.TransportRequestRecords.SingleAsync(x => x.Id == seeded.RequestId);
        var notification = await assertionDb.ProviderNotifications.SingleAsync(x => x.Id == seeded.NotificationId);
        request.Status.Should().Be(TransportRequestStatus.Active);
        notification.State.Should().Be(NotificationDeliveryState.Pending);
        notification.FailedPublishAttempts.Should().Be(1);
        notification.LastFailureCode.Should().Be("publish-failed");
        notification.NextAttemptAt.Should().BeAfter(Now);
    }

    [Fact]
    public async Task Outbox_retries_are_persisted_and_notification_eventually_becomes_dead()
    {
        await EnsureMigrated();
        var seeded = await SeedRouteAndSubmission();

        await RunWorkerUntil(async db =>
            await db.ProviderNotifications.AnyAsync(x => x.Id == seeded.NotificationId &&
                x.State == NotificationDeliveryState.Dead), new AlwaysFailPublisher(), TimeSpan.Zero);

        await using var assertionDb = fixture.CreateDbContext();
        var notification = await assertionDb.ProviderNotifications.SingleAsync(x => x.Id == seeded.NotificationId);
        notification.FailedPublishAttempts.Should().Be(ProviderOutboxWorker.MaximumRetries + 1);
        notification.State.Should().Be(NotificationDeliveryState.Dead);
        notification.NextAttemptAt.Should().BeNull();
        notification.LastFailureCode.Should().Be("publish-failed");
    }

    [Fact]
    public async Task Duplicate_commands_replay_once_and_conflicting_reuse_is_rejected()
    {
        await EnsureMigrated();
        var providerId = Guid.NewGuid();
        var executions = 0;
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProviderCommandIdempotency(db, new FixedTimeProvider(Now));
            var identity = new ProviderIdentity(providerId, "provider-client", new HashSet<string> { "CONTRACT-A" });
            async Task<ProviderCommandResult> Command(CancellationToken _)
            {
                executions++;
                await Task.Yield();
                return new(200, "{\"accepted\":true}");
            }

            var first = await service.ExecuteAsync(identity, "same-key", "journey.status", "JRN-1",
                "{\"status\":1,\"at\":\"now\"}", Guid.NewGuid(), Command, CancellationToken.None);
            var replay = await service.ExecuteAsync(identity, "same-key", "journey.status", "JRN-1",
                "{\"at\":\"now\",\"status\":1}", Guid.NewGuid(), Command, CancellationToken.None);

            first.IsReplay.Should().BeFalse();
            replay.IsReplay.Should().BeTrue();
            replay.Result.Should().Be(first.Result);
            executions.Should().Be(1);
            await FluentActions.Invoking(() => service.ExecuteAsync(identity, "same-key", "journey.status", "JRN-1",
                "{\"status\":2}", Guid.NewGuid(), Command, CancellationToken.None))
                .Should().ThrowAsync<IdempotencyConflictException>();
        }

        await using var assertionDb = fixture.CreateDbContext();
        (await assertionDb.ProviderCommandReceipts.CountAsync(x => x.ProviderId == providerId)).Should().Be(1);
    }

    [Fact]
    public async Task Retrieval_confirmation_is_provider_scoped_and_idempotent()
    {
        await EnsureMigrated();
        var seeded = await SeedRouteAndSubmission(NotificationDeliveryState.Published);
        var retrievedAt = Now.AddMinutes(5);

        await using (var db = fixture.CreateDbContext())
        {
            var tracker = new NotificationRetrievalTracker(db, new FixedTimeProvider(retrievedAt));
            await tracker.MarkRetrievedAsync(seeded.MessageId, Guid.NewGuid(), seeded.PublicId, CancellationToken.None);
            await tracker.MarkRetrievedAsync(seeded.MessageId, seeded.ProviderId, seeded.PublicId, CancellationToken.None);
            await tracker.MarkRetrievedAsync(seeded.MessageId, seeded.ProviderId, seeded.PublicId, CancellationToken.None);
        }

        await using var assertionDb = fixture.CreateDbContext();
        var notification = await assertionDb.ProviderNotifications.SingleAsync(x => x.Id == seeded.NotificationId);
        notification.State.Should().Be(NotificationDeliveryState.Retrieved);
        notification.RetrievedAt.Should().Be(retrievedAt);
        notification.RetrievedByProviderId.Should().Be(seeded.ProviderId);
    }

    [Fact]
    public async Task Provider_authorization_and_concrete_update_enforce_ownership_without_echo_notification()
    {
        await EnsureMigrated();
        var seeded = await SeedRouteAndSubmission(NotificationDeliveryState.Retrieved);
        var authorizer = new OpenIddictClaimsProviderAuthorizer();
        var authorizedPrincipal = Principal(seeded.ProviderId, seeded.ContractCode);
        authorizer.Authorize(authorizedPrincipal, seeded.ProviderId, seeded.ContractCode).Succeeded.Should().BeTrue();
        authorizer.Authorize(Principal(Guid.NewGuid(), seeded.ContractCode), seeded.ProviderId, seeded.ContractCode)
            .Failure.Should().Be(ProviderAuthorizationFailure.Forbidden);
        authorizer.Authorize(Principal(seeded.ProviderId, "OTHER"), seeded.ProviderId, seeded.ContractCode)
            .Failure.Should().Be(ProviderAuthorizationFailure.Forbidden);

        await using (var db = fixture.CreateDbContext())
        {
            var gateway = new TransportProviderResourceGateway(db);
            var identity = authorizer.Authorize(authorizedPrincipal, seeded.ProviderId, seeded.ContractCode).Identity!;
            using var payload = JsonDocument.Parse("{\"reason\":0,\"cancellingParty\":0}");
            var result = await gateway.ExecuteAsync("request.cancel", seeded.PublicId, payload.RootElement,
                identity, Now.AddMinutes(10), CancellationToken.None);
            result.StatusCode.Should().Be(200);
        }

        await using var assertionDb = fixture.CreateDbContext();
        (await assertionDb.TransportRequestRecords.SingleAsync(x => x.Id == seeded.RequestId)).Status
            .Should().Be(TransportRequestStatus.Cancelled);
        (await assertionDb.ProviderNotifications.CountAsync(x => x.EntityPublicId == seeded.PublicId))
            .Should().Be(1, "provider-originated updates must not echo a new notification");
        (await assertionDb.TransportAuditRecords.SingleAsync(x =>
            x.EntityIdentifier == seeded.PublicId && x.Action == "Cancelled")).Source
            .Should().Be(ChangeSource.TransportProvider);
    }

    private async Task EnsureMigrated()
    {
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
    }

    private async Task<SeededSubmission> SeedRouteAndSubmission(
        NotificationDeliveryState state = NotificationDeliveryState.Pending)
    {
        await using var db = fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N");
        var provider = new TransportProvider
        {
            Code = $"P-{suffix}", Name = "Integration Provider", QueueName = $"provider.{suffix}"
        };
        var contract = new TransportContract
        {
            Code = $"C-{suffix}", Description = "Integration Contract"
        };
        db.AddRange(provider, contract);
        db.ProviderContractRoutes.Add(new ProviderContractRoute
        {
            Provider = provider, Contract = contract, ProviderId = provider.Id, ContractId = contract.Id, CreatedAt = Now
        });
        await db.SaveChangesAsync();
        var request = new TransportRequestRecord
        {
            PublicId = $"REQ-{suffix}".ToUpperInvariant(),
            Status = TransportRequestStatus.Active,
            ContractCode = contract.Code,
            ProviderId = provider.Id,
            Requirements = new TransportRequirements(),
            CreatedAt = Now,
            UpdatedAt = Now
        };
        db.TransportRequestRecords.Add(request);
        var outbox = new ProviderOutbox(db, new FixedTimeProvider(Now));
        var notification = await outbox.AddAsync(contract.Code, "TransportRequestCreated",
            IntegrationEntityType.TransportRequest, request.PublicId,
            $"/api/provider/requests/{request.PublicId}", Guid.NewGuid());
        notification.Should().NotBeNull();
        notification!.State = state;
        if (state == NotificationDeliveryState.Published) notification.PublishedAt = Now;
        if (state == NotificationDeliveryState.Retrieved)
        {
            notification.PublishedAt = Now;
            notification.RetrievedAt = Now;
            notification.RetrievedByProviderId = provider.Id;
        }
        await db.SaveChangesAsync();
        return new(request.Id, request.PublicId, provider.Id, contract.Code, notification.Id, notification.MessageId);
    }

    private async Task RunWorkerUntil(
        Func<NagomiDbContext, Task<bool>> condition,
        IProviderNotificationPublisher publisher,
        TimeSpan? retryDelay = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<NagomiDbContext>(options => options
            .UseNpgsql(fixture.PostgreSqlConnectionString)
            .UseOpenIddict());
        services.AddScoped<IProviderIntegrationDb>(provider => provider.GetRequiredService<NagomiDbContext>());
        await using var serviceProvider = services.BuildServiceProvider();
        var options = Options.Create(new ProviderRabbitMqOptions
        {
            RetryDelay = retryDelay ?? TimeSpan.FromMinutes(1),
            PollInterval = TimeSpan.FromMilliseconds(20),
            BatchSize = 100
        });
        var worker = new ProviderOutboxWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), publisher,
            options, new FixedTimeProvider(Now), NullLogger<ProviderOutboxWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await using var db = fixture.CreateDbContext();
                if (await condition(db)) return;
                await Task.Delay(25, timeout.Token);
            }
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    private static ProviderNotification Notification(string queue, string publicId, params string[] sensitive) => new()
    {
        CorrelationId = Guid.NewGuid(),
        ProviderId = Guid.NewGuid(),
        ContractCode = $"CONTRACT-{queue[^8..]}",
        MessageType = "TransportRequestCreated",
        EntityType = IntegrationEntityType.TransportRequest,
        EntityPublicId = publicId,
        RetrievalUrl = $"/api/provider/requests/{publicId}",
        CreatedAt = Now,
        LastFailureCode = string.Join(',', sensitive)
    };

    private static ClaimsPrincipal Principal(Guid providerId, string contract) => new(new ClaimsIdentity(
    [
        new Claim(ProviderClaimTypes.ProviderId, providerId.ToString()),
        new Claim("client_id", "provider-client"),
        new Claim(ProviderClaimTypes.Contract, contract)
    ], "integration-test"));

    private sealed record SeededSubmission(
        Guid RequestId, string PublicId, Guid ProviderId, string ContractCode, Guid NotificationId, Guid MessageId);

    private sealed class AlwaysFailPublisher : IProviderNotificationPublisher
    {
        public Task PublishAsync(ProviderNotification notification, string queueName,
            CancellationToken cancellationToken) => throw new IOException("Simulated broker outage.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
