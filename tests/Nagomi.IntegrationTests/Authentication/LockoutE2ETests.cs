using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Nagomi.IntegrationTests.Authentication;

public sealed class LockoutE2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("nagomi_lockout")
        .WithUsername("nagomi")
        .WithPassword("nagomi")
        .Build();

    public LockoutApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new LockoutApiFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await _postgres.DisposeAsync().AsTask();
    }
}

public sealed class LockoutApiFactory(string postgresConnectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Nagomi", postgresConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }
}

public sealed class LockoutE2ETests(LockoutE2EFixture fixture) : IClassFixture<LockoutE2EFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Account_locks_after_ten_failed_attempts()
    {
        // 10 failed logins against the bootstrap account.
        for (var i = 0; i < 10; i++)
        {
            var failed = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "WrongPassword"),
            }));
            failed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // The 11th attempt with the CORRECT password is still rejected: account locked.
        var locked = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", "admin"),
            new KeyValuePair<string, string>("password", "Admin"),
        }));
        locked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await locked.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("error_description").GetString()!
            .Should().Contain("bloqueada");
    }
}
