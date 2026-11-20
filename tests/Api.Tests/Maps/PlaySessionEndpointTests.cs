using System.Net;
using System.Net.Http.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Maps;

[Collection(PostgresCollection.Name)]
public sealed class PlaySessionEndpointTests(PostgresFixture postgres)
{
    private sealed record WaypointBody(double X, double Y, string Label);

    private sealed record SessionBody(Guid Id, int Number, string Title, bool GmOnly, List<WaypointBody> Waypoints);

    private sealed record LogBody(string Stat, int OldValue, int NewValue, DateTimeOffset ChangedAt);

    private async Task<(WebApplicationFactory<Program> App, HttpClient Client)> StartSignedInAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var client = app.CreateClient();
        await client.LoginAsync();
        return (app, client);
    }

    private static async Task<List<SessionBody>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SessionBody>>("/api/gm/sessions"))!;

    private static async Task<SessionBody> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/gm/sessions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionBody>())!;
    }

    private static Task<HttpResponseMessage> PutRouteAsync(HttpClient client, Guid id, object waypoints) =>
        client.PutAsJsonAsync($"/api/gm/sessions/{id}/waypoints", new { waypoints });

    [Theory]
    [InlineData("GET", "/api/gm/sessions")]
    [InlineData("POST", "/api/gm/sessions")]
    [InlineData("PUT", "/api/gm/sessions/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/gm/sessions/00000000-0000-0000-0000-000000000001/waypoints")]
    [InlineData("DELETE", "/api/gm/sessions/00000000-0000-0000-0000-000000000001")]
    public async Task Endpoints_require_sign_in(string method, string url)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { number = 1 });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Created_session_is_hidden_by_default_with_no_route()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            Assert.Empty(await ListAsync(client));

            var created = await CreateAsync(client, new { number = 8, title = "  The Ruined Tower  " });

            Assert.NotEqual(Guid.Empty, created.Id);
            Assert.Equal((8, "The Ruined Tower", true), (created.Number, created.Title, created.GmOnly));
            Assert.Empty(created.Waypoints);
            var listed = Assert.Single(await ListAsync(client));
            Assert.Equal(created.Id, listed.Id);
        }
    }

    [Fact]
    public async Task Title_is_optional_and_gm_only_can_be_turned_off()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, new { number = 1, gmOnly = false });

            Assert.Equal("", created.Title);
            Assert.False(created.GmOnly);
        }
    }

    [Fact]
    public async Task Sessions_are_listed_in_number_order()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await CreateAsync(client, new { number = 3 });
            await CreateAsync(client, new { number = 1 });
            await CreateAsync(client, new { number = 10 });

            Assert.Equal([1, 3, 10], (await ListAsync(client)).Select(s => s.Number));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task Invalid_number_is_rejected(int? number)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/gm/sessions", new { number });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await ListAsync(client));
        }
    }

    [Fact]
    public async Task Overlong_title_is_rejected()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var response = await client.PostAsJsonAsync("/api/gm/sessions", new { number = 1, title = new string('x', 101) });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Duplicate_numbers_conflict()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await CreateAsync(client, new { number = 1 });
            var two = await CreateAsync(client, new { number = 2 });

            var create = await client.PostAsJsonAsync("/api/gm/sessions", new { number = 1 });
            var update = await client.PutAsJsonAsync($"/api/gm/sessions/{two.Id}", new { number = 1 });

            Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Equal([1, 2], (await ListAsync(client)).Select(s => s.Number));
        }
    }

    [Fact]
    public async Task Put_updates_number_title_and_visibility()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, new { number = 1, title = "Draft" });

            var response = await client.PutAsJsonAsync(
                $"/api/gm/sessions/{created.Id}",
                new { number = 2, title = "The Wedding", gmOnly = false });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<SessionBody>())!;
            Assert.Equal((2, "The Wedding", false), (updated.Number, updated.Title, updated.GmOnly));
            var listed = Assert.Single(await ListAsync(client));
            Assert.Equal((2, "The Wedding", false), (listed.Number, listed.Title, listed.GmOnly));
        }
    }

    [Fact]
    public async Task Put_without_gm_only_keeps_the_current_visibility()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var created = await CreateAsync(client, new { number = 1, gmOnly = false });

            await client.PutAsJsonAsync($"/api/gm/sessions/{created.Id}", new { number = 1, title = "Renamed" });

            Assert.False(Assert.Single(await ListAsync(client)).GmOnly);
        }
    }

    [Fact]
    public async Task Route_is_saved_in_order_with_labels()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 8 });
            object[] route =
            [
                new { x = 0.69, y = 0.488, label = "Stonetop" },
                new { x = 0.565, y = 0.595 },
                new { x = 0, y = 1, label = "  The Ruined Tower  " },
            ];

            var response = await PutRouteAsync(client, session.Id, route);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            List<WaypointBody> expected =
            [
                new(0.69, 0.488, "Stonetop"),
                new(0.565, 0.595, ""),
                new(0, 1, "The Ruined Tower"),
            ];
            Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<SessionBody>())!.Waypoints);
            Assert.Equal(expected, Assert.Single(await ListAsync(client)).Waypoints);
        }
    }

    [Fact]
    public async Task Saving_a_route_replaces_the_previous_one()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });
            await PutRouteAsync(client, session.Id, new[] { new { x = 0.1, y = 0.1 }, new { x = 0.2, y = 0.2 } });

            await PutRouteAsync(client, session.Id, new[] { new { x = 0.5, y = 0.5 } });

            Assert.Equal([new WaypointBody(0.5, 0.5, "")], Assert.Single(await ListAsync(client)).Waypoints);

            await PutRouteAsync(client, session.Id, Array.Empty<object>());

            Assert.Empty(Assert.Single(await ListAsync(client)).Waypoints);
        }
    }

    public static TheoryData<object> InvalidWaypoints => new()
    {
        new { x = -0.01, y = 0.5 },
        new { x = 1.01, y = 0.5 },
        new { x = 0.5, y = -0.01 },
        new { x = 0.5, y = 1.01 },
        new { y = 0.5 },
        new { x = 0.5 },
        new { x = 0.5, y = 0.5, label = new string('x', 101) },
    };

    [Theory]
    [MemberData(nameof(InvalidWaypoints))]
    public async Task Invalid_waypoint_rejects_the_whole_route(object waypoint)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });
            await PutRouteAsync(client, session.Id, new[] { new { x = 0.1, y = 0.1 } });

            var response = await PutRouteAsync(client, session.Id, new[] { new { x = 0.2, y = 0.2 }, waypoint });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal([new WaypointBody(0.1, 0.1, "")], Assert.Single(await ListAsync(client)).Waypoints);
        }
    }

    [Fact]
    public async Task Routes_are_limited_to_200_waypoints()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });
            var point = new { x = 0.5, y = 0.5 };

            Assert.Equal(HttpStatusCode.OK, (await PutRouteAsync(client, session.Id, Enumerable.Repeat(point, 200))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutRouteAsync(client, session.Id, Enumerable.Repeat(point, 201))).StatusCode);
        }
    }

    [Fact]
    public async Task Missing_waypoint_list_is_rejected()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });

            var response = await client.PutAsJsonAsync($"/api/gm/sessions/{session.Id}/waypoints", new { });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Delete_removes_the_session_and_its_route()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });
            await PutRouteAsync(client, session.Id, new[] { new { x = 0.1, y = 0.1 } });

            var response = await client.DeleteAsync($"/api/gm/sessions/{session.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await ListAsync(client));
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/gm/sessions", new { number = 1 })).StatusCode);
        }
    }

    [Theory]
    [InlineData("PUT", "")]
    [InlineData("PUT", "/waypoints")]
    [InlineData("DELETE", "")]
    public async Task Unknown_session_returns_not_found(string method, string suffix)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/gm/sessions/{Guid.NewGuid()}{suffix}");
            if (method == "PUT")
            {
                request.Content = suffix == "" ? JsonContent.Create(new { number = 1 }) : JsonContent.Create(new { waypoints = Array.Empty<object>() });
            }

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Session_changes_do_not_touch_the_stat_log()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var session = await CreateAsync(client, new { number = 1 });
            await PutRouteAsync(client, session.Id, new[] { new { x = 0.1, y = 0.1 } });
            await client.DeleteAsync($"/api/gm/sessions/{session.Id}");

            Assert.Empty((await client.GetFromJsonAsync<List<LogBody>>("/api/gm/steading/log"))!);
        }
    }
}
