using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Persistence;
using Nagomi.Api.Infrastructure.PublicIds;
using Testcontainers.PostgreSql;

namespace Nagomi.IntegrationTests.Infrastructure;

public sealed class PublicIdGeneratorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Generator_produces_sequential_human_readable_ids()
    {
        var options = new DbContextOptionsBuilder<NagomiDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseOpenIddict()
            .Options;
        await using var db = new NagomiDbContext(options);
        await db.Database.MigrateAsync();

        var generator = new PostgresPublicIdGenerator(db);

        var first = await generator.NextAsync("REQ", CancellationToken.None);
        var second = await generator.NextAsync("REQ", CancellationToken.None);
        var batch = await generator.NextBatchAsync("JRN", 3, CancellationToken.None);

        first.Should().Match("REQ-????-??????");
        second.Should().Match("REQ-????-??????");
        string.CompareOrdinal(second, first).Should().BeGreaterThan(0);
        batch.Should().HaveCount(3);
        batch.Should().OnlyContain(id => id.StartsWith("JRN-"));
        batch.Should().OnlyHaveUniqueItems();
    }
}
