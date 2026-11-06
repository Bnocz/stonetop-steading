using Api.Data;
using Api.Domain;
using Api.Steadings;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class SteadingPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private async Task<(string ConnectionString, Guid SteadingId)> CreateSeededDatabaseAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);
        await db.Database.MigrateAsync();
        await SteadingSeeder.SeedAsync(db);
        return (connectionString, (await db.Steadings.SingleAsync()).Id);
    }

    private static async Task ChangeStatAsync(string connectionString, Guid steadingId, SteadingStat stat, int value)
    {
        await using var db = PostgresFixture.CreateContext(connectionString);
        await new SteadingService(db, new FixedTimeProvider()).ChangeStatAsync(steadingId, stat, value);
    }

    private static async Task<(Steading Steading, List<LogEntry> Log)> ReadAsync(string connectionString)
    {
        await using var db = PostgresFixture.CreateContext(connectionString);
        return (await db.Steadings.SingleAsync(), await db.LogEntries.ToListAsync());
    }

    [Fact]
    public async Task Seed_matches_the_stonetop_starting_values()
    {
        var (connectionString, _) = await CreateSeededDatabaseAsync();

        var (steading, log) = await ReadAsync(connectionString);

        Assert.Equal("Stonetop", steading.Name);
        Assert.Equal(
            new SteadingStats(Size: 0, Fortunes: 1, Surplus: 1, Prosperity: -1, Defenses: -1, Population: 0),
            steading.Stats);
        Assert.Empty(steading.Debilities);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Seeding_twice_keeps_one_steading()
    {
        var (connectionString, _) = await CreateSeededDatabaseAsync();

        await using var db = PostgresFixture.CreateContext(connectionString);
        await SteadingSeeder.SeedAsync(db);

        Assert.Equal(1, await db.Steadings.CountAsync());
    }

    [Fact]
    public async Task Valid_change_updates_the_stat_and_writes_one_log_entry()
    {
        var (connectionString, steadingId) = await CreateSeededDatabaseAsync();

        await ChangeStatAsync(connectionString, steadingId, SteadingStat.Fortunes, 3);

        var (steading, log) = await ReadAsync(connectionString);
        Assert.Equal(3, steading.Fortunes);
        var entry = Assert.Single(log);
        Assert.Equal(steadingId, entry.SteadingId);
        Assert.Equal(SteadingStat.Fortunes, entry.Stat);
        Assert.Equal(1, entry.OldValue);
        Assert.Equal(3, entry.NewValue);
        Assert.Equal(Now, entry.ChangedAt);
    }

    [Fact]
    public async Task Out_of_range_change_throws_and_writes_nothing()
    {
        var (connectionString, steadingId) = await CreateSeededDatabaseAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => ChangeStatAsync(connectionString, steadingId, SteadingStat.Fortunes, 4));

        var (steading, log) = await ReadAsync(connectionString);
        Assert.Equal(1, steading.Fortunes);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Setting_the_same_value_writes_no_log_entry()
    {
        var (connectionString, steadingId) = await CreateSeededDatabaseAsync();

        await ChangeStatAsync(connectionString, steadingId, SteadingStat.Fortunes, 1);

        var (_, log) = await ReadAsync(connectionString);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Changing_a_missing_steading_throws()
    {
        var (connectionString, _) = await CreateSeededDatabaseAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => ChangeStatAsync(connectionString, Guid.NewGuid(), SteadingStat.Fortunes, 2));
    }

    [Fact]
    public async Task If_the_log_write_fails_the_stat_does_not_change()
    {
        var (connectionString, steadingId) = await CreateSeededDatabaseAsync();
        await using (var db = PostgresFixture.CreateContext(connectionString))
        {
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "LogEntries" ADD CONSTRAINT "CK_test_reject" CHECK ("NewValue" <> 3)""");
        }

        await Assert.ThrowsAsync<DbUpdateException>(
            () => ChangeStatAsync(connectionString, steadingId, SteadingStat.Fortunes, 3));

        var (steading, log) = await ReadAsync(connectionString);
        Assert.Equal(1, steading.Fortunes);
        Assert.Empty(log);
    }

    [Theory]
    [InlineData(SteadingStat.Size, 3)]
    [InlineData(SteadingStat.Fortunes, 4)]
    [InlineData(SteadingStat.Surplus, -1)]
    [InlineData(SteadingStat.Prosperity, 3)]
    [InlineData(SteadingStat.Defenses, 4)]
    [InlineData(SteadingStat.Population, 3)]
    public async Task Database_rejects_out_of_range_values(SteadingStat stat, int value)
    {
        var (connectionString, _) = await CreateSeededDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);
        var sql = $"""UPDATE "Steadings" SET "{stat}" = {value}""";

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task Saving_over_a_newer_version_throws()
    {
        var (connectionString, _) = await CreateSeededDatabaseAsync();
        await using var first = PostgresFixture.CreateContext(connectionString);
        await using var second = PostgresFixture.CreateContext(connectionString);
        var firstCopy = await first.Steadings.SingleAsync();
        var secondCopy = await second.Steadings.SingleAsync();

        firstCopy.ChangeStat(SteadingStat.Fortunes, 2, Now);
        await first.SaveChangesAsync();
        secondCopy.ChangeStat(SteadingStat.Fortunes, 3, Now);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
