using System.Net;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public sealed class SessionPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Session_survives_a_restart_when_keys_are_in_the_database()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var options = new WebApplicationFactoryClientOptions { HandleCookies = false };

        string cookie;
        await using (var first = GmApp.Create(connectionString: connectionString))
        {
            using var client = first.CreateClient(options);
            var login = await client.LoginAsync();
            cookie = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';')[0];
        }

        await using var second = GmApp.Create(connectionString: connectionString);
        using var secondClient = second.CreateClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gm/me");
        request.Headers.Add("Cookie", cookie);

        var me = await secondClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
