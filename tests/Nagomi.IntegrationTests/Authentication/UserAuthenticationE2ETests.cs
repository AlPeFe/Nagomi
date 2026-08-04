using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nagomi.Api.Infrastructure.Identity;
using Nagomi.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Nagomi.IntegrationTests.Authentication;

public sealed class UserAuthenticationE2EFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("nagomi_auth")
        .WithUsername("nagomi")
        .WithPassword("nagomi")
        .Build();

    public UserAuthApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new UserAuthApiFactory(_postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await _postgres.DisposeAsync().AsTask();
    }

    public NagomiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NagomiDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseOpenIddict()
            .Options;
        return new NagomiDbContext(options);
    }
}

public sealed class UserAuthApiFactory(string postgresConnectionString) : WebApplicationFactory<Program>
{
    public static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), $"nagomi-server-{Guid.NewGuid():N}.log");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Nagomi", postgresConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.ConfigureLogging(logging => logging.AddProvider(new FileLoggerProvider(LogPath)));
    }
}

internal sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(path, categoryName);
    public void Dispose() { }
}

internal sealed class FileLogger(string path, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var line =
            $"{DateTime.UtcNow:O} [{logLevel}] {category}: {formatter(state, exception)}" +
            (exception is not null ? $"\n{exception}" : string.Empty);
        File.AppendAllText(path, line + "\n");
    }
}

public sealed class UserAuthenticationE2ETests(UserAuthenticationE2EFixture fixture)
    : IClassFixture<UserAuthenticationE2EFixture>
{
    private const string AdminEmail = "admin@nagomi.local";
    private const string DefaultEmail = "operator@nagomi.local";
    private const string Password = "Password123";

    private async Task<NagomiDbContext> SeedAsync()
    {
        var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var roleStore = new RoleStore<ApplicationRole, NagomiDbContext, Guid>(db);
        var roleManager = new RoleManager<ApplicationRole>(
            roleStore, null, new UpperInvariantLookupNormalizer(), null, null);
        foreach (var role in NagomiRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                (await roleManager.CreateAsync(new ApplicationRole(role))).Succeeded.Should().BeTrue();
        }

        var userStore = new UserStore<ApplicationUser, ApplicationRole, NagomiDbContext, Guid>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore, null, new PasswordHasher<ApplicationUser>(), null, null,
            new UpperInvariantLookupNormalizer(), null, null, null);

        if (await userManager.FindByEmailAsync(AdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                DisplayName = "Administrador",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            (await userManager.CreateAsync(admin, Password)).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(admin, NagomiRoles.Admin)).Succeeded.Should().BeTrue();
        }
        if (await userManager.FindByEmailAsync(DefaultEmail) is null)
        {
            var op = new ApplicationUser
            {
                UserName = DefaultEmail,
                Email = DefaultEmail,
                DisplayName = "Operador",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            (await userManager.CreateAsync(op, Password)).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(op, NagomiRoles.Default)).Succeeded.Should().BeTrue();
        }

        return db;
    }

    private async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = Password
        });
        var response = await client.PostAsync("/connect/token", content);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Token endpoint returned {(int)response.StatusCode}: {errorBody}");
        }
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("access_token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }

    [Fact]
    public async Task Password_grant_issues_token_and_me_returns_roles()
    {
        await using var db = await SeedAsync();
        var client = fixture.Factory.CreateClient();
        var token = await LoginAsync(client, AdminEmail);

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResponse = await client.SendAsync(me);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        meBody.GetProperty("email").GetString().Should().Be(AdminEmail);
        meBody.GetProperty("roles").EnumerateArray().Select(x => x.GetString())
            .Should().Contain("admin");
    }

    [Fact]
    public async Task Web_endpoints_require_authentication()
    {
        await using var db = await SeedAsync();
        var client = fixture.Factory.CreateClient();
        (await client.GetAsync("/api/operations/journeys")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/transport-requests/drafts", null)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_user_management_requires_admin_role()
    {
        await using var db = await SeedAsync();
        var client = fixture.Factory.CreateClient();

        var defaultToken = await LoginAsync(client, DefaultEmail);
        var forbidden = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", defaultToken);
        (await client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await LoginAsync(client, AdminEmail);
        var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        allowed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var allowedResponse = await client.SendAsync(allowed);
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await allowedResponse.Content.ReadFromJsonAsync<JsonElement>();
        users.EnumerateArray().Select(x => x.GetProperty("email").GetString())
            .Should().Contain(AdminEmail);
    }

    [Fact]
    public async Task Login_rejects_invalid_credentials_and_disabled_users()
    {
        const string disabledEmail = "disabled-test@nagomi.local";
        await using var db = await SeedAsync();
        var client = fixture.Factory.CreateClient();

        using (var bad = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = AdminEmail,
            ["password"] = "WrongPassword99"
        }))
        {
            (await client.PostAsync("/connect/token", bad)).StatusCode
                .Should().Be(HttpStatusCode.BadRequest);
        }

        var userStore = new UserStore<ApplicationUser, ApplicationRole, NagomiDbContext, Guid>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore, null, new PasswordHasher<ApplicationUser>(), null, null,
            new UpperInvariantLookupNormalizer(), null, null, null);
        var disabled = await userManager.FindByEmailAsync(disabledEmail);
        if (disabled is null)
        {
            disabled = new ApplicationUser
            {
                UserName = disabledEmail,
                Email = disabledEmail,
                DisplayName = "Disabled test",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            (await userManager.CreateAsync(disabled, Password)).Succeeded.Should().BeTrue();
        }
        disabled.IsActive = false;
        (await userManager.UpdateAsync(disabled)).Succeeded.Should().BeTrue();

        using (var disabledLogin = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = disabledEmail,
            ["password"] = Password
        }))
        {
            (await client.PostAsync("/connect/token", disabledLogin)).StatusCode
                .Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
