using System.Net;
using System.Net.Http.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Npcs;

[Collection(PostgresCollection.Name)]
public sealed class GmNpcEndpointTests(PostgresFixture postgres)
{
    private sealed record NpcBody(Guid Id, string Name, string Description, string GmNotes, bool GmOnly, bool Archived);

    private sealed record LogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

    private static readonly object Aratis = new
    {
        name = "Aratis",
        description = "Smith by the well.",
        gmNotes = "Owes the Hillfolk a debt.",
        gmOnly = false,
    };

    private async Task<(WebApplicationFactory<Program> App, HttpClient Client)> StartSignedInAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var client = app.CreateClient();
        await client.LoginAsync();
        return (app, client);
    }

    private static async Task<List<NpcBody>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<NpcBody>>("/api/gm/npcs"))!;

    private static async Task<NpcBody> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/gm/npcs", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NpcBody>())!;
    }

    [Theory]
    [InlineData("GET", "/api/gm/npcs")]
    [InlineData("POST", "/api/gm/npcs")]
    [InlineData("PUT", "/api/gm/npcs/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/gm/npcs/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/gm/npcs/00000000-0000-0000-0000-000000000001/restore")]
    public async Task Endpoints_require_sign_in(string method, string url)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(Aratis);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_starts_empty()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            Assert.Empty(await ListAsync(client));
        }
    }

    [Fact]
    public async Task Created_npc_is_returned_with_all_fields()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, Aratis);

            Assert.NotEqual(Guid.Empty, created.Id);
            var expected = new NpcBody(created.Id, "Aratis", "Smith by the well.", "Owes the Hillfolk a debt.", false, false);
            Assert.Equal(expected, created);
            Assert.Equal([expected], await ListAsync(client));
        }
    }

    [Fact]
    public async Task Name_is_trimmed_and_missing_text_fields_become_empty()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, new { name = "  Blind Ellie  " });

            Assert.Equal("Blind Ellie", created.Name);
            Assert.Equal("", created.Description);
            Assert.Equal("", created.GmNotes);
            Assert.False(created.GmOnly);
        }
    }

    [Fact]
    public async Task List_is_sorted_by_name()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await CreateAsync(client, new { name = "Vesna" });
            await CreateAsync(client, new { name = "aratis" });
            await CreateAsync(client, new { name = "Blind Ellie" });

            Assert.Equal(["aratis", "Blind Ellie", "Vesna"], (await ListAsync(client)).Select(n => n.Name));
        }
    }

    public static TheoryData<string?> InvalidNames => new() { null, "", "   ", new string('x', 101) };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Invalid_name_is_rejected_and_nothing_is_saved(string? name)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/gm/npcs", new { name });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ListAsync(client));
        }
    }

    [Theory]
    [InlineData("description", 2001)]
    [InlineData("gmNotes", 4001)]
    public async Task Overlong_text_is_rejected(string field, int length)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var body = new Dictionary<string, object> { ["name"] = "Aratis", [field] = new string('x', length) };

            var response = await client.PostAsJsonAsync("/api/gm/npcs", body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ListAsync(client));
        }
    }

    [Theory]
    [InlineData("description", 2000)]
    [InlineData("gmNotes", 4000)]
    public async Task Text_at_the_limit_is_accepted(string field, int length)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var body = new Dictionary<string, object> { ["name"] = new string('n', 100), [field] = new string('x', length) };

            var response = await client.PostAsJsonAsync("/api/gm/npcs", body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Put_updates_every_field()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, Aratis);

            var response = await client.PutAsJsonAsync(
                $"/api/gm/npcs/{created.Id}",
                new { name = "Aratis the Smith", description = "Moved to the mill.", gmNotes = "Debt paid.", gmOnly = true });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var expected = new NpcBody(created.Id, "Aratis the Smith", "Moved to the mill.", "Debt paid.", true, false);
            Assert.Equal(expected, await response.Content.ReadFromJsonAsync<NpcBody>());
            Assert.Equal([expected], await ListAsync(client));
        }
    }

    [Fact]
    public async Task Put_with_an_invalid_name_changes_nothing()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, Aratis);

            var response = await client.PutAsJsonAsync($"/api/gm/npcs/{created.Id}", new { name = " " });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal([created], await ListAsync(client));
        }
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/restore")]
    public async Task Unknown_npc_returns_not_found(string method, string suffix)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/gm/npcs/{Guid.NewGuid()}{suffix}");
            if (method == "PUT") request.Content = JsonContent.Create(Aratis);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Delete_archives_the_npc_and_restore_brings_it_back()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, Aratis);

            var archive = await client.DeleteAsync($"/api/gm/npcs/{created.Id}");

            Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
            Assert.Equal(created with { Archived = true }, await archive.Content.ReadFromJsonAsync<NpcBody>());
            Assert.Equal([created with { Archived = true }], await ListAsync(client));

            var restore = await client.PostAsync($"/api/gm/npcs/{created.Id}/restore", null);

            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
            Assert.Equal(created, await restore.Content.ReadFromJsonAsync<NpcBody>());
            Assert.Equal([created], await ListAsync(client));
        }
    }

    [Fact]
    public async Task Npc_changes_do_not_touch_the_stat_log()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, Aratis);
            await client.PutAsJsonAsync($"/api/gm/npcs/{created.Id}", new { name = "Aratis the Smith" });
            await client.DeleteAsync($"/api/gm/npcs/{created.Id}");

            Assert.Empty((await client.GetFromJsonAsync<List<LogBody>>("/api/gm/steading/log"))!);
        }
    }
}
