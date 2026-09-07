using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Nagomi.IntegrationTests.Authentication;

public sealed class OnboardingE2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("nagomi_onboarding")
        .WithUsername("nagomi")
        .WithPassword("nagomi")
        .Build();

    public OnboardingApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new OnboardingApiFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await _postgres.DisposeAsync().AsTask();
    }
}

public sealed class OnboardingApiFactory(string postgresConnectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Nagomi", postgresConnectionString);
        // MigrateOnStartup runs the real UserSeeder: creates the bootstrap admin (admin / Admin)
        // with MustChangePassword on the empty database.
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }
}

/// <summary>
/// First-run onboarding flow (single sequential test: the bootstrap exists only once per fresh DB):
/// the bootstrap admin (admin / Admin) can only complete onboarding; onboarding creates the real
/// admin, deactivates the bootstrap and unlocks the application for the new account.
/// </summary>
public sealed class OnboardingE2ETests(OnboardingE2EFixture fixture)
    : IClassFixture<OnboardingE2EFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    private async Task<string?> LoginAsync(string userName, string password)
    {
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", userName),
            new KeyValuePair<string, string>("password", password),
        }));
        if (!response.IsSuccessStatusCode)
            return null;
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("access_token").GetString();
    }

    private HttpClient Authorized(string token)
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Full_onboarding_flow()
    {
        // 1. Bootstrap logs in with admin / Admin.
        var token = await LoginAsync("admin", "Admin");
        token.Should().NotBeNull();

        // 2. /me reports onboarding required.
        var me = await Authorized(token!).GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("onboardingRequired").GetBoolean().Should().BeTrue();

        // 3. Any other endpoint is forbidden for the bootstrap account.
        var blocked = await Authorized(token!).GetAsync("/api/operations/journeys");
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Onboarding creates the real administrator.
        var onboard = await Authorized(token!).PostAsJsonAsync("/api/auth/onboarding", new
        {
            displayName = "Administradora Real",
            email = "jefa@empresa.es",
            userName = "jefa",
            password = "JefaSegura2026!",
        });
        onboard.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. The bootstrap can no longer log in.
        (await LoginAsync("admin", "Admin")).Should().BeNull();

        // 6. The new administrator logs in, has no onboarding pending and holds the admin role.
        var newToken = await LoginAsync("jefa", "JefaSegura2026!");
        newToken.Should().NotBeNull();
        var meNew = await Authorized(newToken!).GetFromJsonAsync<JsonElement>("/api/auth/me");
        meNew.GetProperty("onboardingRequired").GetBoolean().Should().BeFalse();
        meNew.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Contain("admin");

        // 7. The application is usable with the new account.
        var journeys = await Authorized(newToken!).GetAsync("/api/operations/journeys");
        journeys.StatusCode.Should().Be(HttpStatusCode.OK);

        // 8. A stale bootstrap token can no longer run onboarding (account deactivated).
        var second = await Authorized(token!).PostAsJsonAsync("/api/auth/onboarding", new
        {
            displayName = "Admin Dos",
            email = "dos@empresa.es",
            userName = "dos",
            password = "DosSegura2026!",
        });
        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Forbidden);
    }
}
