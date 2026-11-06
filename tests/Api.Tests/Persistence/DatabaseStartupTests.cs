using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class DatabaseStartupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task App_with_a_connection_string_migrates_and_seeds_on_startup()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseSetting("ConnectionStrings:Default", connectionString));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = PostgresFixture.CreateContext(connectionString);
        Assert.Equal("Stonetop", (await db.Steadings.SingleAsync()).Name);
    }
}
