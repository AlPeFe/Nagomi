using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagomi.Api.Domain;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Infrastructure.Persistence;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Nagomi.IntegrationTests.EndToEnd;

public sealed class TransportRequestProviderFlowFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("nagomi_e2e")
        .WithUsername("nagomi")
        .WithPassword("nagomi")
        .Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    public Guid ProviderId { get; } = Guid.NewGuid();
    public string ContractCode { get; } = $"E2E-{Guid.NewGuid():N}".ToUpperInvariant();
    public string QueueName { get; } = $"nagomi.e2e.{Guid.NewGuid():N}";
    public string ClientId { get; } = "e2e-provider-client";
    public EndToEndApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
            var provider = new TransportProvider
            {
                Id = ProviderId,
                Code = $"PROVIDER-{ProviderId:N}",
                Name = "E2E Ambulance Provider",
                QueueName = QueueName
            };
            var contract = new TransportContract
            {
                Code = ContractCode,
                Description = "End-to-end transport contract"
            };
            db.AddRange(provider, contract);
            db.ProviderContractRoutes.Add(new ProviderContractRoute
            {
                Provider = provider,
                Contract = contract,
                ProviderId = provider.Id,
                ContractId = contract.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var rabbitFactory = new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) };
        await using (var connection = await rabbitFactory.CreateConnectionAsync())
        await using (var channel = await connection.CreateChannelAsync())
            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = $"nagomi.e2e.{ProviderId:N}.dead"
                });

        Factory = new EndToEndApiFactory(
            _postgres.GetConnectionString(), _rabbit.GetConnectionString(), ProviderId, ContractCode, ClientId);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    public NagomiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NagomiDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseOpenIddict()
            .Options;
        return new NagomiDbContext(options);
    }

    public async Task<string> GetRabbitMessageAsync(CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(_rabbit.GetConnectionString()) };
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        while (true)
        {
            var delivery = await channel.BasicGetAsync(QueueName, autoAck: true, cancellationToken);
            if (delivery is not null)
                return Encoding.UTF8.GetString(delivery.Body.ToArray());
            await Task.Delay(50, cancellationToken);
        }
    }
}

public sealed class EndToEndApiFactory(
    string postgresConnectionString,
    string rabbitConnectionString,
    Guid providerId,
    string contractCode,
    string clientId) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Nagomi", postgresConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("ProviderIntegration:RabbitMq:Uri", rabbitConnectionString);
        builder.UseSetting("ProviderIntegration:RabbitMq:Exchange", $"nagomi.e2e.{providerId:N}");
        builder.UseSetting("ProviderIntegration:RabbitMq:DeadLetterExchange", $"nagomi.e2e.{providerId:N}.dead");
        builder.UseSetting("ProviderIntegration:RabbitMq:PollInterval", "00:00:00.050");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = EndToEndAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = EndToEndAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, EndToEndAuthenticationHandler>(
                    EndToEndAuthenticationHandler.AuthenticationScheme, _ => { });
            services.AddSingleton(new EndToEndProviderIdentity(providerId, contractCode, clientId));
        });
    }
}

public sealed record EndToEndProviderIdentity(Guid ProviderId, string ContractCode, string ClientId);

public sealed class EndToEndAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    EndToEndProviderIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "EndToEndProvider";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ProviderClaimTypes.ProviderId, identity.ProviderId.ToString()),
            new Claim(ProviderClaimTypes.Contract, identity.ContractCode),
            new Claim("client_id", identity.ClientId),
            new Claim(ClaimTypes.Role, "admin")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
    }
}

public sealed class TransportRequestProviderFlowTests(TransportRequestProviderFlowFixture fixture)
    : IClassFixture<TransportRequestProviderFlowFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Draft_submission_notification_retrieval_provider_completion_audit_and_operations_are_connected()
    {
        using var client = fixture.Factory.CreateClient();
        var serviceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var appointment = new DateTimeOffset(serviceDate.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        var draftSnapshot = new TransportRequestSnapshot(
            new PatientDetails("Ana", "Lopez", "DNI-E2E-SECRET", "CARD-E2E-SECRET", "600123123"),
            new TransportReasonSnapshot("CONSULT", "Consultation"),
            new LocationSnapshot(LocationType.PrivateAddress, street: "Calle Mayor 1", municipality: "Madrid"),
            new LocationSnapshot(LocationType.HealthcareFacility, "Hospital Central", municipality: "Madrid"),
            new TransportRequirements(), fixture.ContractCode, fixture.ProviderId,
            "E2E Ambulance Provider", null, "private clinical note", "provider-visible note");

        var createResponse = await client.PostAsJsonAsync("/api/transport-requests/drafts", draftSnapshot);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        draft.GetProperty("status").GetInt32().Should().Be((int)TransportRequestStatus.Draft);
        var requestId = draft.GetProperty("id").GetGuid();

        var submitResponse = await client.PostAsJsonAsync($"/api/transport-requests/{requestId}/submit/one-off",
            new SubmitOneOffCommand(JourneySchedule.Outbound(appointment, true), null));
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requestPublicId = submitted.GetProperty("publicId").GetString()!;
        var journey = submitted.GetProperty("journeyRecords")[0];
        var journeyPublicId = journey.GetProperty("publicId").GetString()!;

        using var rabbitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var rabbitBody = await fixture.GetRabbitMessageAsync(rabbitTimeout.Token);
        var notification = JsonSerializer.Deserialize<ProviderNotificationMessage>(rabbitBody, JsonOptions)!;
        notification.MessageType.Should().Be("TransportRequestCreated");
        notification.EntityPublicId.Should().Be(requestPublicId);
        notification.ContractCode.Should().Be(fixture.ContractCode);
        notification.RetrievalUrl.Should().Be($"/api/provider/requests/{requestPublicId}");
        rabbitBody.Should().NotContain("Ana").And.NotContain("DNI-E2E-SECRET")
            .And.NotContain("CARD-E2E-SECRET").And.NotContain("Calle Mayor")
            .And.NotContain("clinical").And.NotContain("600123123");

        while (true)
        {
            await using var db = fixture.CreateDbContext();
            if (await db.ProviderNotifications.AnyAsync(x => x.MessageId == notification.MessageId &&
                    x.State == NotificationDeliveryState.Published, rabbitTimeout.Token))
                break;
            await Task.Delay(50, rabbitTimeout.Token);
        }

        var retrievalResponse = await client.GetAsync(
            $"{notification.RetrievalUrl}?messageId={notification.MessageId}");
        retrievalResponse.EnsureSuccessStatusCode();
        var retrieved = await retrievalResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("publicId").GetString().Should().Be(requestPublicId);

        var replacement = new
        {
            origin = new { type = LocationType.PrivateAddress, street = "Calle Mayor 1", municipality = "Madrid" },
            destination = new { type = LocationType.HealthcareFacility, name = "Hospital Central", municipality = "Madrid" },
            requirements = new { mobility = MobilityType.Wheelchair },
            schedule = new
            {
                appointmentAt = appointment,
                scheduledStartAt = appointment.AddHours(-1),
                scheduledPickupAt = (DateTimeOffset?)null,
                pickupTimePending = false
            },
            providerVisibleNotes = "accepted by provider",
            providerReference = "PROVIDER-JOURNEY-42"
        };
        var replaceRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/provider/journeys/{journeyPublicId}")
        {
            Content = JsonContent.Create(replacement)
        };
        replaceRequest.Headers.Add("Idempotency-Key", "e2e-journey-replace");
        (await client.SendAsync(replaceRequest)).EnsureSuccessStatusCode();

        var completedAt = appointment.AddMinutes(30);
        var completeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/provider/journeys/{journeyPublicId}/status")
        {
            Content = JsonContent.Create(new AddJourneyStatusCommand(
                JourneyStatus.Completed, completedAt, "provider-completed-42",
                ChangeSource.TransportProvider, fixture.ClientId, "provider-status-42"))
        };
        completeRequest.Headers.Add("Idempotency-Key", "e2e-completion-command");
        (await client.SendAsync(completeRequest)).EnsureSuccessStatusCode();

        await using (var db = fixture.CreateDbContext())
        {
            var storedNotification = await db.ProviderNotifications.SingleAsync(x => x.MessageId == notification.MessageId);
            storedNotification.State.Should().Be(NotificationDeliveryState.Retrieved);
            storedNotification.RetrievedByProviderId.Should().Be(fixture.ProviderId);

            var storedJourney = await db.JourneyRecords.SingleAsync(x => x.PublicId == journeyPublicId);
            storedJourney.CurrentStatus.Should().Be(JourneyStatus.Completed);
            storedJourney.ActualCompletedAt.Should().Be(completedAt);
            storedJourney.ProviderReference.Should().Be("PROVIDER-JOURNEY-42");
            storedJourney.ExternallyModified.Should().BeTrue();

            var audit = await db.TransportAuditRecords.SingleAsync(x =>
                x.EntityType == "Journey" && x.EntityIdentifier == journeyPublicId && x.Action == "Updated");
            audit.Source.Should().Be(ChangeSource.TransportProvider);
            audit.Actor.Should().Be(fixture.ClientId);
        }

        var operationsResponse = await client.GetAsync(
            $"/api/operations/journeys?from={serviceDate:yyyy-MM-dd}&to={serviceDate:yyyy-MM-dd}&search={journeyPublicId}");
        operationsResponse.EnsureSuccessStatusCode();
        var operationsBody = await operationsResponse.Content.ReadAsStringAsync();
        var rows = JsonDocument.Parse(operationsBody).RootElement;
        rows.GetArrayLength().Should().Be(1);
        var row = rows[0];
        row.GetProperty("journeyPublicId").GetString().Should().Be(journeyPublicId);
        row.GetProperty("status").GetInt32().Should().Be((int)JourneyStatus.Completed);
        row.GetProperty("providerReference").GetString().Should().Be("PROVIDER-JOURNEY-42");
        row.GetProperty("externallyModified").GetBoolean().Should().BeTrue();
        operationsBody.Should().NotContain("DNI-E2E-SECRET").And.NotContain("CARD-E2E-SECRET");
    }
}
