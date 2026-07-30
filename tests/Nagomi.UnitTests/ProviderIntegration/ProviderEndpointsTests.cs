using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Nagomi.Api.Features.ProviderIntegration;

namespace Nagomi.UnitTests.ProviderIntegration;

public sealed class ProviderEndpointsTests
{
    [Fact]
    public async Task Retrieval_OutsideProviderRoute_DoesNotReturnSnapshot()
    {
        var owningProvider = Guid.NewGuid();
        var caller = Principal(Guid.NewGuid(), "CONTRACT-A");
        var gateway = new StubGateway(new ProviderResourceSnapshot(owningProvider, "CONTRACT-A", new { Secret = "hidden" }));

        var result = await ProviderEndpoints.GetRequestAsync(
            "REQ-1", null, caller, gateway, new OpenIddictClaimsProviderAuthorizer(),
            new StubTracker(), CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    [Fact]
    public async Task Retrieval_MarksOnlySuppliedNotificationRetrieved()
    {
        var providerId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var tracker = new StubTracker();

        var result = await ProviderEndpoints.GetRequestAsync(
            "REQ-1", messageId, Principal(providerId, "CONTRACT-A"),
            new StubGateway(new ProviderResourceSnapshot(providerId, "CONTRACT-A", new { Id = "REQ-1" })),
            new OpenIddictClaimsProviderAuthorizer(), tracker, CancellationToken.None);

        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<object>>();
        tracker.Call.Should().Be((messageId, providerId, "REQ-1"));
    }

    private static ClaimsPrincipal Principal(Guid providerId, string contract) => new(
        new ClaimsIdentity(
        [
            new Claim(ProviderClaimTypes.ProviderId, providerId.ToString()),
            new Claim(ProviderClaimTypes.Contract, contract),
            new Claim("client_id", "client")
        ], "Bearer"));

    private sealed class StubTracker : INotificationRetrievalTracker
    {
        public (Guid, Guid, string)? Call { get; private set; }
        public Task MarkRetrievedAsync(Guid messageId, Guid providerId, string entityPublicId, CancellationToken cancellationToken)
        {
            Call = (messageId, providerId, entityPublicId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubGateway(ProviderResourceSnapshot snapshot) : IProviderResourceGateway
    {
        public Task<ProviderResourceSnapshot?> GetRequestAsync(string publicId, CancellationToken cancellationToken) => Task.FromResult<ProviderResourceSnapshot?>(snapshot);
        public Task<ProviderResourceSnapshot?> GetJourneyAsync(string publicId, CancellationToken cancellationToken) => Task.FromResult<ProviderResourceSnapshot?>(snapshot);
        public Task<ProviderResourceAuthorization?> GetRequestAuthorizationAsync(string publicId, CancellationToken cancellationToken) => Task.FromResult<ProviderResourceAuthorization?>(new(snapshot.ProviderId, snapshot.ContractCode));
        public Task<ProviderResourceAuthorization?> GetJourneyAuthorizationAsync(string publicId, CancellationToken cancellationToken) => Task.FromResult<ProviderResourceAuthorization?>(new(snapshot.ProviderId, snapshot.ContractCode));
        public Task<ProviderCommandResult> ExecuteAsync(string commandType, string entityPublicId, JsonElement payload, ProviderIdentity provider, DateTimeOffset acceptedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
