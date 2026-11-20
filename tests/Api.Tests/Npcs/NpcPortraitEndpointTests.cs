using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Tests.Auth;
using Api.Tests.Maps;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Npcs;

[Collection(PostgresCollection.Name)]
public sealed class NpcPortraitEndpointTests(PostgresFixture postgres)
{
    private static readonly byte[] Png = MapImageEndpointTests.Png;

    private static readonly byte[] WebP = [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8, 0x0E, 0x00, 0x00, 0x00];

    private sealed record ShareLinkBody(string Token);

    private sealed record VersionBody(string Version);

    private sealed record NpcBody(Guid Id, string Name, string? PortraitVersion);

    private sealed record PlayerPortraitBody(Guid NpcId, string Version);

    private sealed record LogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

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

    private static async Task<Guid> CreateNpcAsync(HttpClient gm, string name, bool gmOnly = false)
    {
        var response = await gm.PostAsJsonAsync("/api/gm/npcs", new { name, gmOnly });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NpcBody>())!.Id;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient gm, Guid npcId, byte[] bytes, string contentType = "image/webp")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return gm.PutAsync($"/api/gm/npcs/{npcId}/portrait", content);
    }

    private static async Task<List<NpcBody>> ListAsync(HttpClient gm) =>
        (await gm.GetFromJsonAsync<List<NpcBody>>("/api/gm/npcs"))!;

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Gm_endpoints_require_sign_in(string method)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/gm/npcs/{Guid.NewGuid()}/portrait");
        if (method == "PUT")
        {
            request.Content = new ByteArrayContent(Png);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Npcs_start_without_a_portrait()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");

        Assert.Null(Assert.Single(await ListAsync(harness.Gm)).PortraitVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await harness.Gm.GetAsync($"/api/gm/npcs/{id}/portrait")).StatusCode);
    }

    [Fact]
    public async Task Uploaded_portrait_is_served_back_and_listed()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");

        var upload = await UploadAsync(harness.Gm, id, WebP);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var version = (await upload.Content.ReadFromJsonAsync<VersionBody>())!.Version;
        Assert.Equal(version, Assert.Single(await ListAsync(harness.Gm)).PortraitVersion);
        var image = await harness.Gm.GetAsync($"/api/gm/npcs/{id}/portrait");
        Assert.Equal("image/webp", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(WebP, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Replacing_changes_the_version_and_the_file()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        var first = (await (await UploadAsync(harness.Gm, id, WebP)).Content.ReadFromJsonAsync<VersionBody>())!.Version;

        var second = (await (await UploadAsync(harness.Gm, id, Png, "image/png")).Content.ReadFromJsonAsync<VersionBody>())!.Version;

        Assert.NotEqual(first, second);
        Assert.Equal(Png, await harness.Gm.GetByteArrayAsync($"/api/gm/npcs/{id}/portrait"));
    }

    [Fact]
    public async Task Portraits_belong_to_one_npc_each()
    {
        await using var harness = await StartAsync();
        var aratis = await CreateNpcAsync(harness.Gm, "Aratis");
        var vesna = await CreateNpcAsync(harness.Gm, "Vesna");

        await UploadAsync(harness.Gm, aratis, WebP);

        Assert.Equal(HttpStatusCode.NotFound, (await harness.Gm.GetAsync($"/api/gm/npcs/{vesna}/portrait")).StatusCode);
        Assert.Null((await ListAsync(harness.Gm)).Single(n => n.Id == vesna).PortraitVersion);
    }

    [Fact]
    public async Task Remove_deletes_the_portrait_and_is_safe_to_repeat()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        await UploadAsync(harness.Gm, id, WebP);

        Assert.Equal(HttpStatusCode.NoContent, (await harness.Gm.DeleteAsync($"/api/gm/npcs/{id}/portrait")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await harness.Gm.DeleteAsync($"/api/gm/npcs/{id}/portrait")).StatusCode);

        Assert.Null(Assert.Single(await ListAsync(harness.Gm)).PortraitVersion);
        Assert.Equal(HttpStatusCode.NotFound, (await harness.Gm.GetAsync($"/api/gm/npcs/{id}/portrait")).StatusCode);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Unknown_npc_returns_not_found(string method)
    {
        await using var harness = await StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/gm/npcs/{Guid.NewGuid()}/portrait");
        if (method == "PUT")
        {
            request.Content = new ByteArrayContent(Png);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        }

        var response = await harness.Gm.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string, byte[]> NotImages => new()
    {
        { "empty", [] },
        { "text", "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray() },
        { "gif", "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public async Task Files_that_are_not_png_jpeg_or_webp_are_rejected(string kind, byte[] bytes)
    {
        _ = kind;
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");

        var response = await UploadAsync(harness.Gm, id, bytes, "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(Assert.Single(await ListAsync(harness.Gm)).PortraitVersion);
    }

    [Fact]
    public async Task Portraits_over_1_mb_are_rejected_and_1_mb_is_accepted()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        var exact = new byte[1024 * 1024];
        Png.CopyTo(exact, 0);
        var over = new byte[(1024 * 1024) + 1];
        Png.CopyTo(over, 0);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await UploadAsync(harness.Gm, id, over, "image/png")).StatusCode);
        Assert.Null(Assert.Single(await ListAsync(harness.Gm)).PortraitVersion);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(harness.Gm, id, exact, "image/png")).StatusCode);
    }

    [Fact]
    public async Task Players_get_portraits_of_visible_npcs_only()
    {
        await using var harness = await StartAsync();
        var visible = await CreateNpcAsync(harness.Gm, "Aratis");
        var hidden = await CreateNpcAsync(harness.Gm, "Hidden Stranger", gmOnly: true);
        var archived = await CreateNpcAsync(harness.Gm, "Gone Away");
        var bare = await CreateNpcAsync(harness.Gm, "No Portrait");
        foreach (var id in new[] { visible, hidden, archived })
        {
            await UploadAsync(harness.Gm, id, WebP);
        }

        await harness.Gm.DeleteAsync($"/api/gm/npcs/{archived}");

        var portraits = await harness.Player.GetFromJsonAsync<List<PlayerPortraitBody>>($"/api/view/{harness.Token}/npc-portraits");
        var listed = Assert.Single(portraits!);
        Assert.Equal(visible, listed.NpcId);
        Assert.Equal(HttpStatusCode.OK, (await harness.Player.GetAsync($"/api/view/{harness.Token}/npcs/{visible}/portrait")).StatusCode);
        foreach (var id in new[] { hidden, archived, bare, Guid.NewGuid() })
        {
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await harness.Player.GetAsync($"/api/view/{harness.Token}/npcs/{id}/portrait")).StatusCode);
        }
    }

    [Fact]
    public async Task Hiding_an_npc_again_hides_its_portrait()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        await UploadAsync(harness.Gm, id, WebP);

        await harness.Gm.PutAsJsonAsync($"/api/gm/npcs/{id}", new { name = "Aratis", gmOnly = true });

        Assert.Equal("[]", await harness.Player.GetStringAsync($"/api/view/{harness.Token}/npc-portraits"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await harness.Player.GetAsync($"/api/view/{harness.Token}/npcs/{id}/portrait")).StatusCode);
    }

    [Theory]
    [InlineData("npc-portraits")]
    [InlineData("portrait")]
    public async Task Unknown_or_replaced_token_returns_not_found(string resource)
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        await UploadAsync(harness.Gm, id, WebP);
        string Path(string token) => resource == "portrait"
            ? $"/api/view/{token}/npcs/{id}/portrait"
            : $"/api/view/{token}/npc-portraits";

        Assert.Equal(HttpStatusCode.NotFound, (await harness.Player.GetAsync(Path("not-a-token"))).StatusCode);

        await harness.Gm.PostAsync("/api/gm/steading/share-link", null);

        Assert.Equal(HttpStatusCode.NotFound, (await harness.Player.GetAsync(Path(harness.Token))).StatusCode);
    }

    [Fact]
    public async Task Player_portrait_list_is_not_stored_and_images_revalidate()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        await UploadAsync(harness.Gm, id, WebP);

        var list = await harness.Player.GetAsync($"/api/view/{harness.Token}/npc-portraits");
        var image = await harness.Player.GetAsync($"/api/view/{harness.Token}/npcs/{id}/portrait");

        Assert.True(list.Headers.CacheControl?.NoStore);
        Assert.True(image.Headers.CacheControl?.NoCache);
        Assert.NotNull(image.Headers.ETag);
    }

    [Fact]
    public async Task Portrait_changes_do_not_touch_the_stat_log()
    {
        await using var harness = await StartAsync();
        var id = await CreateNpcAsync(harness.Gm, "Aratis");
        await UploadAsync(harness.Gm, id, WebP);
        await harness.Gm.DeleteAsync($"/api/gm/npcs/{id}/portrait");

        Assert.Empty((await harness.Gm.GetFromJsonAsync<List<LogBody>>("/api/gm/steading/log"))!);
    }
}
