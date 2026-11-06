using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Domain;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Api.Tests.Steadings;

[Collection(PostgresCollection.Name)]
public sealed class GmSteadingEndpointTests(PostgresFixture postgres)
{
    private sealed record StatBody(string Stat, int Raw, int Effective, string? Label, int Min, int? Max);

    private sealed record SheetBody(string Name, List<StatBody> Stats, List<string> Debilities);

    private sealed record LogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

    // Changes the row just before EF saves, so the save hits a conflict.
    private sealed class ConflictingEditInterceptor(string connectionString) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Steading>().Any(e => e.State == EntityState.Modified))
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(
                    """UPDATE "Steadings" SET "Surplus" = "Surplus" + 1""",
                    connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }

    private async Task<(WebApplicationFactory<Program> App, HttpClient Client, string ConnectionString)> StartSignedInAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var client = app.CreateClient();
        await client.LoginAsync();
        return (app, client, connectionString);
    }

    private static async Task<SheetBody> GetSheetAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SheetBody>("/api/gm/steading"))!;

    private static async Task<List<LogBody>> GetLogAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LogBody>>("/api/gm/steading/log"))!;

    private static StatBody Find(SheetBody sheet, string stat) => sheet.Stats.Single(s => s.Stat == stat);

    private static Task<HttpResponseMessage> PutStatAsync(HttpClient client, string stat, object body) =>
        client.PutAsJsonAsync($"/api/gm/steading/stats/{stat}", body);

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var db = PostgresFixture.CreateContext(connectionString);
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    [Theory]
    [InlineData("GET", "/api/gm/steading")]
    [InlineData("GET", "/api/gm/steading/log")]
    [InlineData("PUT", "/api/gm/steading/stats/Fortunes")]
    public async Task Endpoints_require_sign_in(string method, string url)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "PUT") request.Content = JsonContent.Create(new { value = 2 });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sheet_matches_the_seed()
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var sheet = await GetSheetAsync(client);

            Assert.Equal("Stonetop", sheet.Name);
            Assert.Empty(sheet.Debilities);
            Assert.Equal(new StatBody("Size", 0, 0, "Village", -1, 2), Find(sheet, "Size"));
            Assert.Equal(new StatBody("Fortunes", 1, 1, null, -3, 3), Find(sheet, "Fortunes"));
            Assert.Equal(new StatBody("Surplus", 1, 1, null, 0, null), Find(sheet, "Surplus"));
            Assert.Equal(new StatBody("Prosperity", -1, -1, "Poor", -2, 2), Find(sheet, "Prosperity"));
            Assert.Equal(new StatBody("Defenses", -1, -1, "Militia", -2, 3), Find(sheet, "Defenses"));
            Assert.Equal(new StatBody("Population", 0, 0, "Steady", -2, 2), Find(sheet, "Population"));
        }
    }

    [Fact]
    public async Task Lacking_shows_in_effective_prosperity_and_its_label_but_not_the_raw_value()
    {
        var (app, client, connectionString) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await ExecuteSqlAsync(connectionString, """UPDATE "Steadings" SET "Lacking" = true""");

            var sheet = await GetSheetAsync(client);

            Assert.Equal(["Lacking"], sheet.Debilities);
            Assert.Equal(new StatBody("Prosperity", -1, -2, "Dirt", -2, 2), Find(sheet, "Prosperity"));
        }
    }

    [Fact]
    public async Task Put_changes_the_stat_and_adds_a_log_entry()
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await PutStatAsync(client, "Fortunes", new { value = 3 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var returned = (await response.Content.ReadFromJsonAsync<SheetBody>())!;
            Assert.Equal(3, Find(returned, "Fortunes").Raw);
            Assert.Equal(3, Find(await GetSheetAsync(client), "Fortunes").Raw);
            var entry = Assert.Single(await GetLogAsync(client));
            Assert.Equal("Fortunes", entry.Stat);
            Assert.Equal(1, entry.OldValue);
            Assert.Equal(3, entry.NewValue);
        }
    }

    [Fact]
    public async Task Out_of_range_value_returns_bad_request_and_changes_nothing()
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await PutStatAsync(client, "Fortunes", new { value = 4 });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, Find(await GetSheetAsync(client), "Fortunes").Raw);
            Assert.Empty(await GetLogAsync(client));
        }
    }

    [Fact]
    public async Task Missing_value_returns_bad_request_and_changes_nothing()
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await PutStatAsync(client, "Fortunes", new { });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, Find(await GetSheetAsync(client), "Fortunes").Raw);
            Assert.Empty(await GetLogAsync(client));
        }
    }

    [Theory]
    [InlineData("Morale")]
    [InlineData("99")]
    [InlineData("3")]
    public async Task Unknown_stat_returns_not_found_and_changes_nothing(string stat)
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await PutStatAsync(client, stat, new { value = 1 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await GetLogAsync(client));
        }
    }

    [Fact]
    public async Task Conflicting_edit_returns_conflict_and_writes_no_log_entry()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString).WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.ConfigureDbContext<SteadingDbContext>(
                options => options.AddInterceptors(new ConflictingEditInterceptor(connectionString)))));
        using var client = app.CreateClient();
        await client.LoginAsync();

        var response = await PutStatAsync(client, "Fortunes", new { value = 3 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, Find(await GetSheetAsync(client), "Fortunes").Raw);
        Assert.Empty(await GetLogAsync(client));
    }

    [Fact]
    public async Task Log_is_newest_first()
    {
        var (app, client, _) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await PutStatAsync(client, "Fortunes", new { value = 2 });
            await PutStatAsync(client, "Surplus", new { value = 5 });

            var log = await GetLogAsync(client);

            Assert.Equal(["Surplus", "Fortunes"], log.Select(entry => entry.Stat));
        }
    }

    [Fact]
    public async Task Log_returns_the_latest_50_entries()
    {
        var (app, client, connectionString) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await using (var db = PostgresFixture.CreateContext(connectionString))
            {
                var steadingId = (await db.Steadings.SingleAsync()).Id;
                var start = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
                for (var i = 0; i < 51; i++)
                {
                    db.LogEntries.Add(new LogEntry(steadingId, SteadingStat.Surplus, i, i + 1, start.AddMinutes(i)));
                }

                await db.SaveChangesAsync();
            }

            var log = await GetLogAsync(client);

            Assert.Equal(50, log.Count);
            Assert.Equal(51, log[0].NewValue);
            Assert.Equal(2, log[^1].NewValue);
        }
    }
}
