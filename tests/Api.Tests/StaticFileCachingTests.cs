using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

public sealed class StaticFileCachingTests : IDisposable
{
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory();

    public StaticFileCachingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot.FullName, "index.html"), "<div id=\"root\"></div>");
        _webRoot.CreateSubdirectory("assets");
        File.WriteAllText(Path.Combine(_webRoot.FullName, "assets", "index-abc123.js"), "// built");
    }

    public void Dispose() => _webRoot.Delete(recursive: true);

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot.FullName));

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/some/client/route")]
    public async Task Browsers_must_revalidate_the_page(string url)
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoCache, $"{url} should be sent with Cache-Control: no-cache.");
    }

    [Fact]
    public async Task Built_assets_are_not_forced_to_revalidate()
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/assets/index-abc123.js");

        response.EnsureSuccessStatusCode();
        Assert.NotEqual(true, response.Headers.CacheControl?.NoCache);
    }
}
