using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Api.Tests.Steadings;

[Collection(PostgresCollection.Name)]
public sealed class PlayerViewEndpointTests(PostgresFixture postgres)
{
    private sealed record ShareLinkBody(string Token);

    private sealed record PlayerStatBody(string Stat, int Value, string? Label);

    private sealed record PlayerSheetBody(string Name, List<PlayerStatBody> Stats, List<string> Debilities);

    private sealed record PlayerLogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

    private sealed class Harness : IAsyncDisposable
    {
        public required WebApplicationFactory<Program> App { get; init; }

        public required HttpClient Gm { get; init; }

        public required HttpClient Player { get; init; }

        public required string ConnectionString { get; init; }

        public required string Token { get; init; }

        public async ValueTask DisposeAsync()
        {
            Gm.Dispose();
            Player.Dispose();
            await App.DisposeAsync();
        }
    }

    private async Task<Harness> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var gm = app.CreateClient();
        await gm.LoginAsync();
        var link = await gm.GetFromJsonAsync<ShareLinkBody>("/api/gm/steading/share-link");
        return new Harness
        {
            App = app,
            Gm = gm,
            Player = app.CreateClient(),
            ConnectionString = connectionString,
            Token = link!.Token,
        };
    }

    private static PlayerStatBody Find(PlayerSheetBody sheet, string stat) => sheet.Stats.Single(s => s.Stat == stat);

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var db = PostgresFixture.CreateContext(connectionString);
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Sheet_works_without_signing_in()
    {
        await using var harness = await StartAsync();

        var sheet = await harness.Player.GetFromJsonAsync<PlayerSheetBody>($"/api/view/{harness.Token}/steading");

        Assert.Equal("Stonetop", sheet!.Name);
        Assert.Empty(sheet.Debilities);
        Assert.Equal(new PlayerStatBody("Size", 0, "Village"), Find(sheet, "Size"));
        Assert.Equal(new PlayerStatBody("Fortunes", 1, null), Find(sheet, "Fortunes"));
        Assert.Equal(new PlayerStatBody("Surplus", 1, null), Find(sheet, "Surplus"));
        Assert.Equal(new PlayerStatBody("Prosperity", -1, "Poor"), Find(sheet, "Prosperity"));
    }

    [Fact]
    public async Task Lacking_shows_as_effective_prosperity_and_its_label()
    {
        await using var harness = await StartAsync();
        await ExecuteSqlAsync(harness.ConnectionString, """UPDATE "Steadings" SET "Lacking" = true""");

        var sheet = await harness.Player.GetFromJsonAsync<PlayerSheetBody>($"/api/view/{harness.Token}/steading");

        Assert.Equal(["Lacking"], sheet!.Debilities);
        Assert.Equal(new PlayerStatBody("Prosperity", -2, "Dirt"), Find(sheet, "Prosperity"));
    }

    [Fact]
    public async Task Sheet_returns_only_the_allowed_fields()
    {
        await using var harness = await StartAsync();

        var text = await harness.Player.GetStringAsync($"/api/view/{harness.Token}/steading");

        using var json = JsonDocument.Parse(text);
        Assert.Equal(["debilities", "name", "stats"], PropertyNames(json.RootElement));
        Assert.All(
            json.RootElement.GetProperty("stats").EnumerateArray(),
            stat => Assert.Equal(["label", "stat", "value"], PropertyNames(stat)));
        Assert.DoesNotContain(harness.Token, text);
    }

    [Fact]
    public async Task Log_works_without_signing_in_and_returns_only_the_allowed_fields()
    {
        await using var harness = await StartAsync();
        await harness.Gm.PutAsJsonAsync("/api/gm/steading/stats/Fortunes", new { value = 2 });

        var text = await harness.Player.GetStringAsync($"/api/view/{harness.Token}/log");

        var entry = Assert.Single(JsonSerializer.Deserialize<List<PlayerLogBody>>(text, JsonSerializerOptions.Web)!);
        Assert.Equal("Fortunes", entry.Stat);
        Assert.Equal(1, entry.OldValue);
        Assert.Equal(2, entry.NewValue);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(
            ["changedAt", "newValue", "oldValue", "stat"],
            PropertyNames(json.RootElement.EnumerateArray().Single()));
    }

    [Fact]
    public async Task Log_returns_the_latest_50_entries_newest_first()
    {
        await using var harness = await StartAsync();
        await ExecuteSqlAsync(
            harness.ConnectionString,
            """
            INSERT INTO "LogEntries" ("Id", "SteadingId", "Stat", "OldValue", "NewValue", "ChangedAt")
            SELECT gen_random_uuid(), s."Id", 'Surplus', n - 1, n, TIMESTAMPTZ '2026-11-06 12:00:00+00' + n * INTERVAL '1 minute'
            FROM "Steadings" s, generate_series(1, 51) AS n
            """);

        var log = await harness.Player.GetFromJsonAsync<List<PlayerLogBody>>($"/api/view/{harness.Token}/log");

        Assert.Equal(50, log!.Count);
        Assert.Equal(51, log[0].NewValue);
        Assert.Equal(2, log[^1].NewValue);
    }

    [Theory]
    [InlineData("steading")]
    [InlineData("log")]
    public async Task Unknown_token_returns_not_found(string resource)
    {
        await using var harness = await StartAsync();

        var response = await harness.Player.GetAsync($"/api/view/not-a-real-token/{resource}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "stats/Fortunes")]
    [InlineData("POST", "steading")]
    [InlineData("PUT", "steading")]
    [InlineData("DELETE", "steading")]
    [InlineData("POST", "log")]
    public async Task View_routes_are_read_only(string method, string path)
    {
        await using var harness = await StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/view/{harness.Token}/{path}")
        {
            Content = JsonContent.Create(new { value = 3 }),
        };

        var response = await harness.Player.SendAsync(request);

        Assert.False(response.IsSuccessStatusCode);
        var sheet = await harness.Player.GetFromJsonAsync<PlayerSheetBody>($"/api/view/{harness.Token}/steading");
        Assert.Equal(1, Find(sheet!, "Fortunes").Value);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Share_link_endpoints_require_sign_in(string method)
    {
        await using var harness = await StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/gm/steading/share-link");

        var response = await harness.Player.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Replacing_the_link_kills_the_old_one()
    {
        await using var harness = await StartAsync();

        var response = await harness.Gm.PostAsync("/api/gm/steading/share-link", null);

        response.EnsureSuccessStatusCode();
        var replacement = await response.Content.ReadFromJsonAsync<ShareLinkBody>();
        Assert.NotEqual(harness.Token, replacement!.Token);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await harness.Player.GetAsync($"/api/view/{harness.Token}/steading")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await harness.Player.GetAsync($"/api/view/{replacement.Token}/steading")).StatusCode);
    }

    [Fact]
    public async Task Replacing_the_link_writes_no_log_entry()
    {
        await using var harness = await StartAsync();

        await harness.Gm.PostAsync("/api/gm/steading/share-link", null);

        var log = await harness.Gm.GetFromJsonAsync<List<PlayerLogBody>>("/api/gm/steading/log");
        Assert.Empty(log!);
    }

    [Fact]
    public async Task Tokens_are_long_url_safe_and_random()
    {
        await using var first = await StartAsync();
        await using var second = await StartAsync();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", first.Token);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Theory]
    [InlineData("steading")]
    [InlineData("log")]
    public async Task View_responses_are_not_stored(string resource)
    {
        await using var harness = await StartAsync();

        var response = await harness.Player.GetAsync($"/api/view/{harness.Token}/{resource}");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Migration_gives_existing_steadings_unique_tokens_and_keeps_their_stats()
    {
        const string previousMigration = "20261106212334_AddDataProtectionKeys";
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var db = PostgresFixture.CreateContext(connectionString);
        await db.GetService<IMigrator>().MigrateAsync(previousMigration);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Steadings" ("Id", "Name", "Size", "Fortunes", "Surplus", "Prosperity", "Defenses", "Population", "Diminished", "Lacking", "Malcontent")
            VALUES (gen_random_uuid(), 'First', 0, 2, 5, -1, -1, 0, false, false, false),
                   (gen_random_uuid(), 'Second', 0, 1, 1, -1, -1, 0, false, false, false)
            """);

        await db.Database.MigrateAsync();

        var steadings = await db.Steadings.AsNoTracking().ToListAsync();
        Assert.Equal(2, steadings.Count);
        Assert.All(steadings, steading => Assert.False(string.IsNullOrWhiteSpace(steading.ShareToken)));
        Assert.NotEqual(steadings[0].ShareToken, steadings[1].ShareToken);
        var first = steadings.Single(steading => steading.Name == "First");
        Assert.Equal(2, first.Fortunes);
        Assert.Equal(5, first.Surplus);
    }
}
