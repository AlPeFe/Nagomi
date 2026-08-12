using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nagomi.Api.Features.IdentityAdministration;
using Nagomi.Api.Infrastructure.Persistence;
using Nagomi.IntegrationTests.Infrastructure;
using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nagomi.IntegrationTests.Authentication;

public sealed class IdentityAdministrationTests(ProviderInfrastructureFixture fixture)
    : IClassFixture<ProviderInfrastructureFixture>
{
    [Fact]
    public async Task Create_consumer_client_is_confidential_and_accepts_secret()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NagomiDbContext>();
        await db.Database.MigrateAsync();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = $"consumer-{Guid.NewGuid():N}";

        var result = await IdentityAdministrationEndpoints.CreateClientAsync(
            new(clientId, "Mutua Pepe API"), applications, CancellationToken.None);
        var created = await Response<ApiClientSecretResponse>(result, 201, provider);
        created.ClientSecret.Should().NotBeNullOrWhiteSpace();

        var application = await applications.FindByClientIdAsync(clientId);
        application.Should().NotBeNull();
        (await applications.GetClientTypeAsync(application!)!).Should().Be(ClientTypes.Confidential);
        (await applications.ValidateClientSecretAsync(application!, created.ClientSecret)).Should().BeTrue();
        (await applications.HasPermissionAsync(application!, Permissions.GrantTypes.ClientCredentials)).Should().BeTrue();
        var properties = await applications.GetPropertiesAsync(application!);
        properties["nagomi_api_consumer"].ValueKind.Should().Be(JsonValueKind.True);
    }

    [Fact]
    public async Task Consumer_clients_can_be_listed_and_non_consumers_are_excluded()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NagomiDbContext>();
        await db.Database.MigrateAsync();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var consumerId = $"consumer-{Guid.NewGuid():N}";
        await IdentityAdministrationEndpoints.CreateClientAsync(
            new(consumerId, "Consumer"), applications, CancellationToken.None);

        // A provider integration client (no consumer marker) must not appear in the list.
        var providerId = $"provider-{Guid.NewGuid():N}";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = providerId,
            ClientSecret = "provider-secret",
            ClientType = ClientTypes.Confidential
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        await applications.CreateAsync(descriptor);

        var result = await IdentityAdministrationEndpoints.ListClientsAsync(applications, CancellationToken.None);
        var list = await Response<IReadOnlyList<ApiClientRow>>(result, 200, provider);
        list.Should().Contain(x => x.ClientId == consumerId);
        list.Should().NotContain(x => x.ClientId == providerId);
        list.Single(x => x.ClientId == consumerId).DisplayName.Should().Be("Consumer");
    }

    [Fact]
    public async Task Rotate_secret_invalidates_old_secret_and_prevents_token()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NagomiDbContext>();
        await db.Database.MigrateAsync();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var clientId = $"consumer-{Guid.NewGuid():N}";

        var createdResult = await IdentityAdministrationEndpoints.CreateClientAsync(
            new(clientId, "Consumer"), applications, CancellationToken.None);
        var created = await Response<ApiClientSecretResponse>(createdResult, 201, provider);
        var application = await applications.FindByClientIdAsync(clientId);
        var applicationId = await applications.GetIdAsync(application!);
        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            Status = Statuses.Valid,
            Subject = clientId,
            Type = TokenTypeHints.AccessToken
        });
        var tokenId = await tokens.GetIdAsync(token);

        var rotatedResult = await IdentityAdministrationEndpoints.RotateSecretAsync(
            clientId, new("new-secret"), applications, tokens, CancellationToken.None);
        var rotated = await Response<ApiClientSecretResponse>(rotatedResult, 200, provider);
        rotated.ClientSecret.Should().Be("new-secret");
        (await applications.ValidateClientSecretAsync(application!, created.ClientSecret)).Should().BeFalse();
        (await applications.ValidateClientSecretAsync(application!, rotated.ClientSecret)).Should().BeTrue();
        (await TokenIsInvalid(provider, tokenId!)).Should().BeTrue(
            "tokens issued before secret rotation must be revoked");
    }

    [Fact]
    public async Task Revoking_consumer_client_deletes_it_and_revokes_tokens()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NagomiDbContext>();
        await db.Database.MigrateAsync();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var clientId = $"consumer-{Guid.NewGuid():N}";

        await IdentityAdministrationEndpoints.CreateClientAsync(
            new(clientId, "Consumer"), applications, CancellationToken.None);
        var application = await applications.FindByClientIdAsync(clientId);
        var applicationId = await applications.GetIdAsync(application!);
        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            Status = Statuses.Valid,
            Subject = clientId,
            Type = TokenTypeHints.AccessToken
        });
        var tokenId = await tokens.GetIdAsync(token);

        await using var revokeScope = provider.CreateAsyncScope();
        var revokeApplications = revokeScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var revokedResult = await IdentityAdministrationEndpoints.RevokeClientAsync(
            clientId, revokeApplications, CancellationToken.None);
        await Response<object?>(revokedResult, 204, provider);
        (await revokeApplications.FindByClientIdAsync(clientId)).Should().BeNull();
        (await TokenIsInvalid(provider, tokenId!)).Should().BeTrue();
    }

    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddDbContext<NagomiDbContext>(options => options
            .UseNpgsql(fixture.PostgreSqlConnectionString)
            .UseOpenIddict());
        services.AddOpenIddict().AddCore(options => options
            .UseEntityFrameworkCore()
            .UseDbContext<NagomiDbContext>());
        return services.BuildServiceProvider();
    }

    private static async Task<bool> TokenIsInvalid(IServiceProvider services, string tokenId)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var token = await manager.FindByIdAsync(tokenId);
        return token is null || await manager.HasStatusAsync(token, Statuses.Revoked);
    }

    private static async Task<T> Response<T>(IResult result, int expectedStatusCode, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await result.ExecuteAsync(context);
        context.Response.StatusCode.Should().Be(expectedStatusCode);
        if (expectedStatusCode == 204)
            return default!;
        body.Position = 0;
        return (await JsonSerializer.DeserializeAsync<T>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }
}
