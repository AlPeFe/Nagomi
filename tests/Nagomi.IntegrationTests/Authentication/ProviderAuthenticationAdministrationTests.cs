using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.Persistence;
using Nagomi.IntegrationTests.Infrastructure;
using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nagomi.IntegrationTests.Authentication;

public sealed class ProviderAuthenticationAdministrationTests(ProviderInfrastructureFixture fixture)
    : IClassFixture<ProviderInfrastructureFixture>
{
    [Fact]
    public async Task Create_rotate_and_revoke_manage_secrets_and_existing_tokens()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NagomiDbContext>();
        await db.Database.MigrateAsync();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var clientId = $"provider-{Guid.NewGuid():N}";
        var providerId = Guid.NewGuid();

        var createdResult = await ProviderAuthenticationAdministrationEndpoints.CreateClientAsync(
            new(clientId, providerId, [" contract-a ", "CONTRACT-A", "contract-b"],
                ProviderClaimTypes.Operator), applications, CancellationToken.None);
        var created = await Response<ProviderAuthenticationClientSecretResponse>(createdResult, 201, provider);
        created.ClientSecret.Should().NotBeNullOrWhiteSpace();
        created.Contracts.Should().Equal("CONTRACT-A", "CONTRACT-B");

        var application = await applications.FindByClientIdAsync(clientId);
        application.Should().NotBeNull();
        (await applications.ValidateClientSecretAsync(application!, created.ClientSecret)).Should().BeTrue();
        var properties = await applications.GetPropertiesAsync(application!);
        properties["nagomi:provider_id"].GetString().Should().Be(providerId.ToString());
        var applicationId = await applications.GetIdAsync(application!);
        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            Status = Statuses.Valid,
            Subject = clientId,
            Type = TokenTypeHints.AccessToken
        });
        var tokenId = await tokens.GetIdAsync(token);

        var rotatedResult = await ProviderAuthenticationAdministrationEndpoints.RotateSecretAsync(
            clientId, new("replacement-secret"), applications, tokens, CancellationToken.None);
        var rotated = await Response<ProviderAuthenticationClientSecretResponse>(rotatedResult, 200, provider);
        rotated.ClientSecret.Should().Be("replacement-secret");
        (await applications.ValidateClientSecretAsync(application!, created.ClientSecret)).Should().BeFalse();
        (await applications.ValidateClientSecretAsync(application!, rotated.ClientSecret)).Should().BeTrue();
        (await TokenIsInvalid(provider, tokenId!)).Should().BeTrue(
            "token-entry validation must reject tokens issued before secret rotation");

        var secondToken = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            Status = Statuses.Valid,
            Subject = clientId,
            Type = TokenTypeHints.AccessToken
        });
        var secondTokenId = await tokens.GetIdAsync(secondToken);
        await using var revokeScope = provider.CreateAsyncScope();
        var revokeApplications = revokeScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var revokedResult = await ProviderAuthenticationAdministrationEndpoints.RevokeClientAsync(
            clientId, revokeApplications, CancellationToken.None);
        await Response<object?>(revokedResult, 204, provider);
        (await TokenIsInvalid(provider, secondTokenId!)).Should().BeTrue();
        (await revokeApplications.FindByClientIdAsync(clientId)).Should().BeNull();
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
