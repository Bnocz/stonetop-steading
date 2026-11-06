using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Auth;

public sealed class AuthEndpointTests
{
    private sealed record MeBody(string Username);

    private static readonly WebApplicationFactoryClientOptions NoCookies = new() { HandleCookies = false };

    [Fact]
    public async Task Correct_login_signs_the_gm_in()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient();

        var login = await client.LoginAsync();
        var me = await client.GetAsync("/api/gm/me");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(GmApp.Username, (await me.Content.ReadFromJsonAsync<MeBody>())?.Username);
    }

    [Theory]
    [InlineData("wrong", GmApp.Password)]
    [InlineData(GmApp.Username, "wrong")]
    [InlineData("", "")]
    public async Task Wrong_credentials_are_rejected(string username, string password)
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient();

        var login = await client.LoginAsync(username, password);
        var me = await client.GetAsync("/api/gm/me");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Login_with_missing_fields_is_rejected()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient();

        var login = await client.PostAsJsonAsync("/api/gm/login", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Unconfigured_credentials_reject_every_login()
    {
        await using var app = GmApp.Create(username: null, password: null);
        using var client = app.CreateClient();

        var empty = await client.LoginAsync("", "");
        var guess = await client.LoginAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, guess.StatusCode);
    }

    [Fact]
    public async Task Me_without_a_cookie_returns_unauthorized_without_redirecting()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var me = await client.GetAsync("/api/gm/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient();
        await client.LoginAsync();

        var logout = await client.PostAsync("/api/gm/logout", null);
        var me = await client.GetAsync("/api/gm/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Login_cookie_is_http_only_strict_and_secure_outside_development()
    {
        await using var app = GmApp.Create(environment: "Production");
        using var client = app.CreateClient(NoCookies);

        var login = await client.LoginAsync();

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_cookie_lasts_after_the_browser_closes()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient(NoCookies);

        var login = await client.LoginAsync();

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eleventh_login_in_a_minute_is_rate_limited()
    {
        await using var app = GmApp.Create();
        using var client = app.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var allowed = await client.LoginAsync("wrong", "wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limited = await client.LoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_returns_not_found()
    {
        var webRoot = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), "<div id=\"root\"></div>");
            await using var app = GmApp.Create().WithWebHostBuilder(
                builder => builder.UseWebRoot(webRoot.FullName));
            using var client = app.CreateClient();

            var apiRoute = await client.GetAsync("/api/does-not-exist");
            var clientRoute = await client.GetAsync("/some/client/route");

            Assert.Equal(HttpStatusCode.NotFound, apiRoute.StatusCode);
            Assert.Equal(HttpStatusCode.OK, clientRoute.StatusCode);
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }
}
