using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;

namespace Api.Tests.Npcs;

[Collection(PostgresCollection.Name)]
public sealed class PlayerNpcEndpointTests(PostgresFixture postgres)
{
    private const string SecretNotes = "SECRET-NOTES-do-not-show";

    private sealed record ShareLinkBody(string Token);

    private sealed record NpcBody(Guid Id, string Name);

    private sealed record PlayerNpcBody(Guid Id, string Name, string Description);

    private async Task<(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> App, HttpClient Gm, HttpClient Player, string Token)> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var gm = app.CreateClient();
        await gm.LoginAsync();
        var link = await gm.GetFromJsonAsync<ShareLinkBody>("/api/gm/steading/share-link");
        return (app, gm, app.CreateClient(), link!.Token);
    }

    private static async Task<NpcBody> CreateAsync(HttpClient gm, string name, bool gmOnly = false, string description = "")
    {
        var response = await gm.PostAsJsonAsync(
            "/api/gm/npcs",
            new { name, description, gmNotes = SecretNotes, gmOnly });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NpcBody>())!;
    }

    [Fact]
    public async Task Players_see_visible_npcs_sorted_by_name_with_description()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var vesna = await CreateAsync(gm, "Vesna", description: "Runs the Pavilion.");
            var aratis = await CreateAsync(gm, "Aratis", description: "Smith.");

            var npcs = await player.GetFromJsonAsync<List<PlayerNpcBody>>($"/api/view/{token}/npcs");

            Assert.Equal(
                [new PlayerNpcBody(aratis.Id, "Aratis", "Smith."), new PlayerNpcBody(vesna.Id, "Vesna", "Runs the Pavilion.")],
                npcs);
        }
    }

    [Fact]
    public async Task Gm_only_and_archived_npcs_are_never_returned()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            await CreateAsync(gm, "Hidden Stranger", gmOnly: true);
            var archived = await CreateAsync(gm, "Gone Away");
            await gm.DeleteAsync($"/api/gm/npcs/{archived.Id}");
            await CreateAsync(gm, "Aratis");

            var raw = await player.GetStringAsync($"/api/view/{token}/npcs");

            Assert.DoesNotContain("Hidden Stranger", raw);
            Assert.DoesNotContain("Gone Away", raw);
            Assert.Contains("Aratis", raw);
        }
    }

    [Fact]
    public async Task Hiding_an_npc_after_creating_it_removes_it_from_the_player_view()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var created = await CreateAsync(gm, "Aratis");
            await gm.PutAsJsonAsync($"/api/gm/npcs/{created.Id}", new { name = "Aratis", gmOnly = true });

            var npcs = await player.GetFromJsonAsync<List<PlayerNpcBody>>($"/api/view/{token}/npcs");

            Assert.Empty(npcs!);
        }
    }

    [Fact]
    public async Task Response_has_only_player_fields_and_no_gm_notes()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            await CreateAsync(gm, "Aratis");

            var raw = await player.GetStringAsync($"/api/view/{token}/npcs");

            Assert.DoesNotContain(SecretNotes, raw);
            using var json = JsonDocument.Parse(raw);
            var npc = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(
                ["description", "id", "name"],
                npc.EnumerateObject().Select(p => p.Name).Order());
        }
    }

    [Fact]
    public async Task Unknown_or_replaced_token_returns_not_found()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            await CreateAsync(gm, "Aratis");

            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync("/api/view/not-a-token/npcs")).StatusCode);

            await gm.PostAsync("/api/gm/steading/share-link", null);

            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/view/{token}/npcs")).StatusCode);
        }
    }

    [Fact]
    public async Task Response_is_not_stored_by_the_browser()
    {
        var (app, gm, player, token) = await StartAsync();
        await using (app)
        using (gm)
        using (player)
        {
            var response = await player.GetAsync($"/api/view/{token}/npcs");

            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }
}
