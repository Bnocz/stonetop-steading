using System.Net;
using System.Net.Http.Json;
using Api.Domain;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Steadings;

[Collection(PostgresCollection.Name)]
public sealed class ReadableShareLinkTests(PostgresFixture postgres)
{
    private const string Url = "/api/gm/steading/share-link/readable";

    private sealed record ShareLinkBody(string Token);

    private sealed record LogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

    private async Task<(WebApplicationFactory<Program> App, HttpClient Gm, HttpClient Player)> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var gm = app.CreateClient();
        await gm.LoginAsync();
        return (app, gm, app.CreateClient());
    }

    private static async Task<string> MakeReadableAsync(HttpClient gm)
    {
        var response = await gm.PostAsync(Url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShareLinkBody>())!.Token;
    }

    [Fact]
    public void Word_list_has_512_unique_short_lowercase_words()
    {
        Assert.Equal(512, ShareLinkWords.All.Count);
        Assert.Equal(512, ShareLinkWords.All.Distinct().Count());
        Assert.All(ShareLinkWords.All, word => Assert.Matches("^[a-z]{2,8}$", word));
    }

    [Fact]
    public async Task Requires_sign_in()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();

        var response = await client.PostAsync(Url, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Makes_a_link_of_four_listed_words()
    {
        var (app, gm, player) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var token = await MakeReadableAsync(gm);

            Assert.Matches("^[a-z]+(-[a-z]+){3}$", token);
            Assert.InRange(token.Length, 12, 64);
            Assert.All(token.Split('-'), word => Assert.Contains(word, ShareLinkWords.All));
        }
    }

    [Fact]
    public async Task The_readable_link_works_and_the_old_one_stops()
    {
        var (app, gm, player) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var old = (await gm.GetFromJsonAsync<ShareLinkBody>("/api/gm/steading/share-link"))!.Token;

            var token = await MakeReadableAsync(gm);

            Assert.Equal(token, (await gm.GetFromJsonAsync<ShareLinkBody>("/api/gm/steading/share-link"))!.Token);
            Assert.Equal(HttpStatusCode.OK, (await player.GetAsync($"/api/view/{token}/steading")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/view/{old}/steading")).StatusCode);
        }
    }

    [Fact]
    public async Task Each_readable_link_is_new()
    {
        var (app, gm, player) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var first = await MakeReadableAsync(gm);
            var second = await MakeReadableAsync(gm);

            Assert.NotEqual(first, second);
            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/view/{first}/steading")).StatusCode);
        }
    }

    [Fact]
    public async Task Replace_link_still_makes_a_long_random_link_afterwards()
    {
        var (app, gm, player) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            await MakeReadableAsync(gm);

            var response = await gm.PostAsync("/api/gm/steading/share-link", null);

            Assert.Matches("^[A-Za-z0-9_-]{43}$", (await response.Content.ReadFromJsonAsync<ShareLinkBody>())!.Token);
        }
    }

    [Fact]
    public async Task Does_not_write_to_the_stat_log()
    {
        var (app, gm, player) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            await MakeReadableAsync(gm);

            Assert.Empty((await gm.GetFromJsonAsync<List<LogBody>>("/api/gm/steading/log"))!);
        }
    }
}
