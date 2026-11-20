using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Maps;

[Collection(PostgresCollection.Name)]
public sealed class PlayerMapEndpointTests(PostgresFixture postgres)
{
    private const string SecretTitle = "SECRET-TITLE-hidden-session";
    private const string SecretLabel = "SECRET-LABEL-hidden-stop";

    private sealed record ShareLinkBody(string Token);

    private sealed record ImageBody(string Version);

    private sealed record MapBody(ImageBody? Image);

    private sealed record WaypointBody(double X, double Y, string Label);

    private sealed record SessionBody(Guid Id, int Number, string Title, List<WaypointBody> Waypoints);

    private sealed class Harness : IAsyncDisposable
    {
        public required WebApplicationFactory<Program> App { get; init; }

        public required HttpClient Gm { get; init; }

        public required HttpClient Player { get; init; }

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
        return new Harness { App = app, Gm = gm, Player = app.CreateClient(), Token = link!.Token };
    }

    private static async Task<Guid> CreateSessionAsync(
        HttpClient gm,
        int number,
        string title,
        bool gmOnly,
        params object[] waypoints)
    {
        var response = await gm.PostAsJsonAsync("/api/gm/sessions", new { number, title, gmOnly });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<SessionBody>())!.Id;
        (await gm.PutAsJsonAsync($"/api/gm/sessions/{id}/waypoints", new { waypoints })).EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task Without_a_map_players_get_no_image()
    {
        await using var harness = await StartAsync();

        var map = await harness.Player.GetFromJsonAsync<MapBody>($"/api/view/{harness.Token}/map");

        Assert.Null(map!.Image);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await harness.Player.GetAsync($"/api/view/{harness.Token}/map/image")).StatusCode);
    }

    [Fact]
    public async Task Players_get_the_uploaded_map_without_signing_in()
    {
        await using var harness = await StartAsync();
        var uploaded = await MapImageEndpointTests.UploadAsync(harness.Gm, MapImageEndpointTests.Png);
        var version = (await uploaded.Content.ReadFromJsonAsync<ImageBody>())!.Version;

        var map = await harness.Player.GetFromJsonAsync<MapBody>($"/api/view/{harness.Token}/map");
        var image = await harness.Player.GetAsync($"/api/view/{harness.Token}/map/image");

        Assert.Equal(version, map!.Image!.Version);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(MapImageEndpointTests.Png, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Players_see_only_visible_sessions_in_number_order_with_their_routes()
    {
        await using var harness = await StartAsync();
        var two = await CreateSessionAsync(harness.Gm, 2, "Home Again", gmOnly: false, new { x = 0.3, y = 0.4, label = "Stonetop" });
        await CreateSessionAsync(harness.Gm, 3, SecretTitle, gmOnly: true, new { x = 0.9, y = 0.9, label = SecretLabel });
        var one = await CreateSessionAsync(harness.Gm, 1, "Setting Out", gmOnly: false, new { x = 0.1, y = 0.2 }, new { x = 0.2, y = 0.3 });

        var sessions = (await harness.Player.GetFromJsonAsync<List<SessionBody>>($"/api/view/{harness.Token}/sessions"))!;

        Assert.Equal([one, two], sessions.Select(s => s.Id));
        Assert.Equal([1, 2], sessions.Select(s => s.Number));
        Assert.Equal(["Setting Out", "Home Again"], sessions.Select(s => s.Title));
        Assert.Equal([new WaypointBody(0.1, 0.2, ""), new WaypointBody(0.2, 0.3, "")], sessions[0].Waypoints);
        Assert.Equal([new WaypointBody(0.3, 0.4, "Stonetop")], sessions[1].Waypoints);
    }

    [Fact]
    public async Task Hidden_sessions_never_reach_players()
    {
        await using var harness = await StartAsync();
        await CreateSessionAsync(harness.Gm, 1, SecretTitle, gmOnly: true, new { x = 0.5, y = 0.5, label = SecretLabel });

        var raw = await harness.Player.GetStringAsync($"/api/view/{harness.Token}/sessions");

        Assert.Equal("[]", raw);
    }

    [Fact]
    public async Task Hiding_a_session_again_removes_it_from_the_player_view()
    {
        await using var harness = await StartAsync();
        var id = await CreateSessionAsync(harness.Gm, 1, SecretTitle, gmOnly: false, new { x = 0.5, y = 0.5, label = SecretLabel });

        await harness.Gm.PutAsJsonAsync($"/api/gm/sessions/{id}", new { number = 1, title = SecretTitle, gmOnly = true });
        var raw = await harness.Player.GetStringAsync($"/api/view/{harness.Token}/sessions");

        Assert.DoesNotContain(SecretTitle, raw);
        Assert.DoesNotContain(SecretLabel, raw);
    }

    [Fact]
    public async Task Session_responses_have_only_player_fields()
    {
        await using var harness = await StartAsync();
        await CreateSessionAsync(harness.Gm, 1, "Setting Out", gmOnly: false, new { x = 0.5, y = 0.5, label = "Stonetop" });

        var raw = await harness.Player.GetStringAsync($"/api/view/{harness.Token}/sessions");

        using var json = JsonDocument.Parse(raw);
        var session = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(["id", "number", "title", "waypoints"], session.EnumerateObject().Select(p => p.Name).Order());
        var waypoint = Assert.Single(session.GetProperty("waypoints").EnumerateArray());
        Assert.Equal(["label", "x", "y"], waypoint.EnumerateObject().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData("map")]
    [InlineData("map/image")]
    [InlineData("sessions")]
    public async Task Unknown_or_replaced_token_returns_not_found(string resource)
    {
        await using var harness = await StartAsync();
        await MapImageEndpointTests.UploadAsync(harness.Gm, MapImageEndpointTests.Png);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await harness.Player.GetAsync($"/api/view/not-a-token/{resource}")).StatusCode);

        await harness.Gm.PostAsync("/api/gm/steading/share-link", null);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await harness.Player.GetAsync($"/api/view/{harness.Token}/{resource}")).StatusCode);
    }

    [Theory]
    [InlineData("map")]
    [InlineData("sessions")]
    public async Task Map_data_is_not_stored_by_the_browser(string resource)
    {
        await using var harness = await StartAsync();

        var response = await harness.Player.GetAsync($"/api/view/{harness.Token}/{resource}");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Image_is_revalidated_with_an_etag()
    {
        await using var harness = await StartAsync();
        await MapImageEndpointTests.UploadAsync(harness.Gm, MapImageEndpointTests.Png);
        var first = await harness.Player.GetAsync($"/api/view/{harness.Token}/map/image");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/view/{harness.Token}/map/image");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await harness.Player.SendAsync(request);

        Assert.True(first.Headers.CacheControl?.NoCache);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }
}
